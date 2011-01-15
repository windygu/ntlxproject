#region usings

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using ASC.Common.Module;
using ASC.Notify;
using ASC.Notify.Messages;
using ASC.Notify.Model;
using ASC.Notify.Patterns;
using ASC.Notify.Recipients;

#endregion

namespace ASC.Core.Notify
{
    public abstract class NotifySource : INotifySource, IDependencyProvider
    {
        private readonly IModuleInfo moduleInfo;
        private bool initialized;
        private readonly object syncRoot = new object();

        private readonly IDictionary<CultureInfo, IActionProvider> actions = new Dictionary<CultureInfo, IActionProvider>();

        private readonly IDictionary<CultureInfo, IPatternProvider> patterns = new Dictionary<CultureInfo, IPatternProvider>();

        private readonly IDictionary<CultureInfo, IActionPatternProvider> actionPatterns = new Dictionary<CultureInfo, IActionPatternProvider>();

        protected ISubscriptionProvider SubscriprionProvider;

        protected IRecipientProvider RecipientsProvider;

        protected IDependencyProvider DependencyProvider;

        protected IActionProvider ActionProvider
        {
            get { return GetActionProvider(); }
        }

        protected IPatternProvider PatternProvider
        {
            get { return GetPatternProvider(); }
        }

        protected IActionPatternProvider ActionPatternProvider
        {
            get { return GetActionPatternProvider(); }
        }

        public NotifySource(IModuleInfo moduleInfo)
            : this(
                moduleInfo, moduleInfo != null ? moduleInfo.ID.ToString() : null,
                moduleInfo != null ? moduleInfo.Name : null)
        {
        }

        public NotifySource(IModuleInfo moduleInfo, string id)
            : this(moduleInfo, id, moduleInfo != null ? moduleInfo.Name : null)
        {
        }

        public NotifySource(IModuleInfo moduleInfo, string id, string name)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException("id");
            this.moduleInfo = moduleInfo;
            ID = id;
            Name = name;
        }

        #region INotifySource

        public string ID { get; private set; }

        public string Name { get; private set; }

        public IActionPatternProvider GetActionPatternProvider()
        {
            lock (actionPatterns)
            {
                CultureInfo culture = Thread.CurrentThread.CurrentCulture;
                if (!actionPatterns.ContainsKey(culture))
                {
                    actionPatterns[culture] = CreateActionPatternProvider();
                }
                return actionPatterns[culture];
            }
        }

        public IActionProvider GetActionProvider()
        {
            lock (actions)
            {
                CultureInfo culture = Thread.CurrentThread.CurrentCulture;
                if (!actions.ContainsKey(culture))
                {
                    actions[culture] = CreateActionProvider();
                }
                return actions[culture];
            }
        }

        public IPatternProvider GetPatternProvider()
        {
            lock (patterns)
            {
                CultureInfo culture = Thread.CurrentThread.CurrentCulture;
                if (!patterns.ContainsKey(culture))
                {
                    patterns[culture] = CreatePatternsProvider();
                }
                return patterns[culture];
            }
        }

        public IDependencyProvider GetDependencyProvider()
        {
            LazyInitializeProviders();
            return DependencyProvider;
        }

        public IRecipientProvider GetRecipientsProvider()
        {
            LazyInitializeProviders();
            return RecipientsProvider;
        }

        public ISubscriptionProvider GetSubscriptionProvider()
        {
            LazyInitializeProviders();
            return SubscriprionProvider;
        }

        public ISubscriptionSource GetSubscriptionSource()
        {
            LazyInitializeProviders();
            return SubscriprionProvider;
        }

        #endregion

        protected void LazyInitializeProviders()
        {
            if (!initialized)
            {
                lock (syncRoot)
                {
                    if (!initialized)
                    {
                        Initialize();
                    }
                }
            }
        }

        private void Initialize()
        {
            RecipientsProvider = CreateRecipientsProvider();
            if (RecipientsProvider == null)
                throw new NotifyException(
                    String.Format(Resource.NotifyException_Message_ProviderNotInstanced,
                                  "IRecipientsProvider"));
            DependencyProvider = CreateDependencyProvider();
            if (DependencyProvider == null)
                throw new NotifyException(
                    String.Format(Resource.NotifyException_Message_ProviderNotInstanced,
                                  "IDependencyProvider"));
            SubscriprionProvider = CreateSubscriptionProvider();
            if (SubscriprionProvider == null)
                throw new NotifyException(
                    String.Format(Resource.NotifyException_Message_ProviderNotInstanced,
                                  "ISubscriprionProvider"));
            initialized = true;
        }

        #region virtual methods

        protected abstract IActionPatternProvider CreateActionPatternProvider();

        protected abstract IPatternProvider CreatePatternsProvider();

        protected abstract IActionProvider CreateActionProvider();

        protected virtual ISubscriptionProvider CreateSubscriptionProvider()
        {
            var directSubscriptionProvider = new DirectSubscriptionProvider(ID, CoreContext.SubscriptionManager,
                                                                            RecipientsProvider, ActionProvider);
            return new TopSubscriptionProvider(RecipientsProvider, directSubscriptionProvider,
                                               Array.ConvertAll(WorkContext.DefaultClientSenders, (sm) => sm.ID));
        }

        protected virtual IDependencyProvider CreateDependencyProvider()
        {
            return new FakeDepProvider();
        }

        private class FakeDepProvider : IDependencyProvider
        {
            private static readonly ITagValue[] _fake = new ITagValue[0];

            public ITagValue[] GetDependencies(INoticeMessage message, string objectID, ITag[] tags)
            {
                return _fake;
            }
        }

        protected virtual IRecipientProvider CreateRecipientsProvider()
        {
            return new RecipientProviderImpl();
        }

        #endregion

        #region IDependencyProvider Members

        public virtual ITagValue[] GetDependencies(INoticeMessage message, string objectID, ITag[] tags)
        {
            return new ITagValue[] { };
        }

        #endregion
    }
}