namespace e2Kindle.Aspects
{
    using System;
    using System.Threading;

    using PostSharp.Aspects;

    [Serializable]
    public class OnWorkerThreadAttribute : MethodInterceptionAspect
    {
        public override void OnInvoke(MethodInterceptionArgs args)
        {
            ThreadPool.QueueUserWorkItem(state => args.Proceed());
        }
    }
}