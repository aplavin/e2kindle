namespace e2Kindle.Aspects
{
    using System;
    using PostSharp.Aspects;
    using PostSharp.Aspects.Dependencies;

    using e2Kindle.UI;

    [Serializable]
    [ProvideAspectRole(StandardRoles.Tracing)]
    [AspectRoleDependency(AspectDependencyAction.Order, AspectDependencyPosition.After, StandardRoles.ExceptionHandling)]
    public class UseSetWaitAttribute : OnMethodBoundaryAspect
    {
        public UseSetWaitAttribute()
        {
            AttributePriority = -1;
            AspectPriority = -1;
        }

        public override void OnEntry(MethodExecutionArgs args)
        {
            MainWindow.SetWait(true);
        }

        public override void OnExit(MethodExecutionArgs args)
        {
            MainWindow.SetWait(false);
        }
    }
}