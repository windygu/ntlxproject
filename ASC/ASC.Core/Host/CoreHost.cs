using System;
using System.Collections.Generic;
using System.Net;
using ASC.Common.Module;
using ASC.Common.Services;
using ASC.Core.Common.CoreTalk;
using ASC.Core.Common.Services;
using ASC.Core.Configuration.Module;
using ASC.Core.Hosting;
using ASC.Core.Users.Module;
using ASC.Net;
using ASC.Reflection;
using log4net;
using ConfConst = ASC.Core.Configuration.Constants;
using UsersConst = ASC.Core.Users.Constants;

namespace ASC.Core.Host
{
	public class CoreHost : ServicesModulePartHostBase, ICoreHost
	{
		private readonly IDictionary<Guid, CoreSMPEntry> coreServiceParts = new Dictionary<Guid, CoreSMPEntry>();
		private WebServiceEntryPoint wsEntryPoint;

		public CoreHost()
			: this(0)
		{

		}

		public CoreHost(int fixedPort)
			: base(ASC.Core.Hosting.Constants.CoreHostServiceInfo)
		{
			FixedPort = 0 < fixedPort ? fixedPort : TcpPortResolver.GetFreePort();

			_BuildCache();

			WorkContext.AddCoreAddressResolver(
				new FixedCoreAddressResolver(ConnectionHostEntry.GetLocalhost(FixedPort)),
				FixedCoreAddressResolver.Priority
			);

			CoreContext.Configuration.SecureCorePort = TcpPortResolver.GetFreePort();
			LogManager.GetLogger("ASC.Core.Host").InfoFormat("{0} - SecureCorePort", CoreContext.Configuration.SecureCorePort);

			EnableWebService = false;
			WebServicePort = 0;
		}

		public event HostStatusChangeEventEventHandler HostStatusChange;

		#region IServicesModulePartsHost

        protected override void BeforeAddPart(Guid servicesModulePartID)
		{
			if (!coreServiceParts.ContainsKey(servicesModulePartID))
				throw new ModulePartNotFoundException(servicesModulePartID);
		}

		protected override void FillModulePartInfo(Guid servicesModulePartID)
		{
			CoreSMPEntry coreEntry = coreServiceParts[servicesModulePartID];
			SMPEntry entry = this[servicesModulePartID];

			entry.ServiceModulePartInfo = coreEntry.ServiceModulePartInfo;
		}

		protected override void FillModulePartInstance(Guid servicesModulePartID)
		{
			CoreSMPEntry coreEntry = coreServiceParts[servicesModulePartID];
			SMPEntry entry = this[servicesModulePartID];

			entry.ModuleServicesPart = coreEntry.GetServiceModulePartInstance();
		}

		public override void OnHostStatusChange(object sender, HostStatusChangeEventArgs e)
		{
			if (HostStatusChange != null) HostStatusChange(sender, e);

			base.OnHostStatusChange(sender, e);
		}

		public override void OnServiceStatusChange(object sender, ServiceStatusChangeEventArgs e)
		{
			FireEvent(sender, e);
			base.OnServiceStatusChange(sender, e);
		}

		public override void OnServiceException(object sender, ServiceExceptionEventArgs e)
		{
			FireEvent(sender, e);
			base.OnServiceException(sender, e);
		}

		#endregion

		#region ServiceController

		protected override void BeforeStartWork()
		{
			//System.Runtime.Remoting.Services.TrackingServices.RegisterTrackingHandler(new RemotingTrackingHandler());
		}

		protected override void AfterStartWork()
		{
			ControllerStart();

			SecurityContext.AuthenticateMe(ConfConst.CoreSystem);

			if (EnableWebService)
			{
				if (WebServicePort == 0) WebServicePort = TcpPortResolver.GetFreePort();
				wsEntryPoint = new WebServiceEntryPoint();
				wsEntryPoint.Start(WebServicePort, "AscWSEntryPoint");
			}
		}

		protected override void BeforeStopWork()
		{
			if (wsEntryPoint != null)
			{
				wsEntryPoint.Stop();
				wsEntryPoint = null;
			}

			ControllerStop();
		}

		protected override void DoWork()
		{
			do { } while (!Sleep(new TimeSpan(0, 0, 1)));
		}

		#endregion

		class CoreSMPEntry
		{
			public IModulePartInfo ServiceModulePartInfo;

			Type ModuleType;
			IModule _ModuleInstance;
			IModuleServicesPart ServiceModulePartInstance = null;

			public CoreSMPEntry(IModulePartInfo partInfo, Type moduleType)
			{
				ServiceModulePartInfo = partInfo;
				ModuleType = moduleType;
			}

			public IModule GetModuleInstance()
			{
				if (_ModuleInstance == null)
					_ModuleInstance = TypeInstance.Create(ModuleType) as IModule;

				return _ModuleInstance;
			}

			public IModuleServicesPart GetServiceModulePartInstance()
			{
				if (ServiceModulePartInstance == null)
				{
					foreach (var part in GetModuleInstance().Parts)
					{
						if (part.Info == ServiceModulePartInfo && part is IModuleServicesPart)
						{
							ServiceModulePartInstance = part as IModuleServicesPart;
							break;
						}
					}
				}
				return ServiceModulePartInstance;
			}
		}

		private void _BuildCache()
		{
			_AddCoreSMPEntry(ConfConst.ModulePartInfo_Services, typeof(ConfigurationModule));
			_AddCoreSMPEntry(UsersConst.ModulePartInfo_Services, typeof(UsersModule));
		}

		private void _AddCoreSMPEntry(IModulePartInfo partInfo, Type moduleType)
		{
			coreServiceParts.Add(partInfo.ID, new CoreSMPEntry(partInfo, moduleType));
		}

        public int FixedPort
		{
			get;
			private set;
		}

		public bool EnableWebService
		{
			get;
			set;
		}

		public int WebServicePort
		{
			get { return webServicePort; }
			set
			{
				if (wsEntryPoint != null) throw new InvalidOperationException(string.Format("Web Service already started at {0} port", webServicePort));
				if (value < 0) throw new ArgumentOutOfRangeException("WebServicePort", value, "Must be more or equal 0");
				webServicePort = value;
			}
		}

		private int webServicePort;
	}
}