using System;using ASC.Common.Cache;using ASC.Common.Security.Authentication;namespace ASC.Core.Security.Authentication {						internal class SessionTicketCache : SimpleCache<SessionTicket, SessionTicketCache.TicketAccountEntry> {		private readonly TimeSpan shiftTimeSpan = TimeSpan.FromTicks(0);								public SessionTicketCache()			: this(CoreContext.Configuration.CoreSyncTimeSpan) {		}								internal SessionTicketCache(TimeSpan shiftTimeSpan)			: base(typeof(HashtableCacheSource)) {			this.shiftTimeSpan = shiftTimeSpan;						AddAddin(				new TimeExpiriedCacheAddin<SessionTicket, SessionTicketCache.TicketAccountEntry>(					TicketExpiried,					new ExpirationCheckStrategy() {												StrategyType = ExpirationCheckStrategy.TimeCheckStrategy.ByAddDate,												CheckStrategy = ExpirationCheckStrategy.CheckStartegy.OnAveryAction,												CheckPeriod = TimeSpan.FromSeconds(1),												CheckInPeriod = TimeSpan.FromSeconds(1)					}				)			);		}		private bool TicketExpiried(SessionTicketCache.TicketAccountEntry ticket, TimeExpiriedCacheAddin<SessionTicket, SessionTicketCache.TicketAccountEntry>.ElmentMetrics metrics) {									return ticket.Ticket.ElapsedTime + TimeSpan.FromSeconds(5) <= DateTime.Now + shiftTimeSpan;		}		public class TicketAccountEntry {			public SessionTicket Ticket {				get;				set;			}			public IAccount Account {				get;				set;			}			public TicketAccountEntry() {			}			public TicketAccountEntry(SessionTicket ticket, IAccount account) {				Ticket = ticket;				Account = account;			}		}	}}