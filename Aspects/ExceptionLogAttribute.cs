namespace e2Kindle.Aspects
{
    using System;
    using System.Reflection;
    using NLog;
    using PostSharp.Aspects;


    [Serializable]
    public sealed class ExceptionLogAttribute : OnExceptionAspect
    {
        [NonSerialized]
        private Logger logger;

        public ExceptionLogAttribute()
        {
            AttributePriority = 0;
            AspectPriority = 0;
        }

        public override void RuntimeInitialize(MethodBase method)
        {
            logger = LogManager.GetLogger("{0}.{1}_Exceptions".FormatWith(method.DeclaringType.FullName, method.Name));
        }

        public override void OnException(MethodExecutionArgs args)
        {
            logger.ErrorException("Exception occured in the method: " + args.Exception.GetType(), args.Exception);
        }
    }
}