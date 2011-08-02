namespace e2Kindle.Aspects
{
    using System;
    using System.Windows.Threading;

    using PostSharp.Aspects;
    using PostSharp.Aspects.Dependencies;

    [Serializable]
    [ProvideAspectRole(StandardRoles.Threading)]
    public class OnGuiThreadAttribute : MethodInterceptionAspect
    {

        public override void OnInvoke(MethodInterceptionArgs eventArgs)
        {
            DispatcherObject dispatcherObject =(DispatcherObject)eventArgs.Instance;

            if (dispatcherObject.CheckAccess())
            {
                // We are already in the GUI thread. Proceed.
                eventArgs.Proceed();
            }
            else
            {
                // Invoke the target method synchronously.  
                dispatcherObject.Dispatcher.Invoke(new Action(eventArgs.Proceed));
            }
        }
    }
}