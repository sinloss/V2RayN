﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

namespace v2rayN.HttpProxyHandler
{
    class SysAction
    {

        [ServiceContract(SessionMode = SessionMode.Required, CallbackContract = typeof(ICallback))]
        public interface IService
        {
            [OperationContract(IsOneWay = true)]
            void Connect();

            [OperationContract(IsOneWay = true)]
            void Switch(Mode.Config config);
        }

        public interface ICallback
        {
            [OperationContract(IsOneWay = true)]
            void Call();
        }
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession)]
    class ActionServer : SysAction.IService
    {
        public static SysAction.ICallback Cb { get; set; }
        public void Connect() {
            Cb = OperationContext.Current.GetCallbackChannel<SysAction.ICallback>();
        }

        public void Switch(Mode.Config config) {
            HttpProxyHandler.HttpProxyHandle.Update(config, false);
        }

        public static void Start() {
            var host = new ServiceHost(typeof(ActionServer), new Uri("net.pipe://localhost"));
            host.AddServiceEndpoint(typeof(SysAction.IService), new NetNamedPipeBinding(), "ACTION");
            host.Open();
        }
    }

    class ActionClient : SysAction.ICallback
    {
        public static SysAction.IService Svr { get; set;}
        public static InstanceContext Ctx { get; set; }
        public void Call()
        {
        }

        public static void Start()
        {
            Ctx = new InstanceContext(new ActionClient());
            Svr = new DuplexChannelFactory<SysAction.IService>(
                Ctx, 
                new NetNamedPipeBinding(),
                new EndpointAddress("net.pipe://localhost/ACTION")).CreateChannel();
            Svr.Connect();
        }
    }
}
