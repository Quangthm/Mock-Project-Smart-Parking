namespace UserService.Application.Common.Models.MessageBroker
{
    /// <summary>
    /// MassTransit constant
    /// </summary>
    public static class MassTransitConstant
    {
        /// <summary>
        /// Queue Exchange type
        /// </summary>
        public enum ExchangeType
        {
            /// <summary>
            /// The fanout
            /// </summary>
            Fanout,

            /// <summary>
            /// The direct
            /// </summary>
            Direct,

            /// <summary>
            /// The topic
            /// </summary>
            Topic,

            /// <summary>
            /// The headers
            /// </summary>
            Headers
        }

        /// <summary>
        /// Queue and exchange
        /// </summary>
        public static class QueueAndExchange
        {
            /// <summary>
            /// The login queue
            /// </summary>
            public const string LoginQueue = "login-queue";

            /// <summary>
            /// The login exchange
            /// </summary>
            public const string LoginExchange = "login-exchange";
        }
    }
}
