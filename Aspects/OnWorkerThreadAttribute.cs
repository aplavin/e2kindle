namespace e2Kindle.Aspects
{
    using System;
    using System.Threading;

    using PostSharp.Aspects;
    using PostSharp.Aspects.Dependencies;

    [Serializable]
    [ProvideAspectRole(StandardRoles.Threading)]
    public class OnWorkerThreadAttribute : MethodInterceptionAspect
    {
        public override void OnInvoke(MethodInterceptionArgs args)
        {
            ThreadPool.QueueUserWorkItem(state => args.Proceed());
        }
    }
}