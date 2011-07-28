namespace e2Kindle.Aspects
{
    using System;
    using e2Kindle.UI;
    using PostSharp.Aspects;

    [Serializable]
    public sealed class ExceptionDialogAttribute : OnExceptionAspect
    {
        public ExceptionDialogAttribute()
        {
            AttributePriority = 1;
            AspectPriority = 1;
        }

        public override void OnException(MethodExecutionArgs args)
        {
            Dialogs.ShowException(args.Exception);
            args.FlowBehavior = FlowBehavior.Continue;
        }
    }
}