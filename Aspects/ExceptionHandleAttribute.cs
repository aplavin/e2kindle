namespace e2Kindle.Aspects
{
    using System;
    using System.Reflection;
    using NLog;
    using PostSharp.Aspects;
    using PostSharp.Aspects.Dependencies;

    using e2Kindle.UI;

    [Flags]
    public enum ExceptionHandling
    {
        None,
        Log,
        Dialog
    }

    [Serializable]
    [ProvideAspectRole(StandardRoles.ExceptionHandling)]
    public sealed class ExceptionHandleAttribute : OnExceptionAspect
    {
        [NonSerialized]
        private Logger _logger;

        private readonly ExceptionHandling _exceptionHandling;

        public ExceptionHandleAttribute(ExceptionHandling exceptionHandling)
        {
            _exceptionHandling = exceptionHandling;
        }

        public override void RuntimeInitialize(MethodBase method)
        {
            _logger = LogManager.GetLogger("{0}.{1}_Exceptions".FormatWith(method.DeclaringType.FullName, method.Name));
        }

        public override void OnException(MethodExecutionArgs args)
        {
            if (_exceptionHandling.HasFlag(ExceptionHandling.Log)) _logger.ErrorException("Exception occured in the method: " + args.Exception.Message, args.Exception);
            if (_exceptionHandling.HasFlag(ExceptionHandling.Dialog)) Dialogs.ShowException(args.Exception);
            args.FlowBehavior = FlowBehavior.Continue;
        }
    }
}