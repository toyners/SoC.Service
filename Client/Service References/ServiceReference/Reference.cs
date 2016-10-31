﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Jabberwocky.SoC.Client.ServiceReference {
    
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(ConfigurationName="ServiceReference.IServiceProvider", CallbackContract=typeof(Jabberwocky.SoC.Client.ServiceReference.IServiceProviderCallback), SessionMode=System.ServiceModel.SessionMode.Required)]
    public interface IServiceProvider {
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IServiceProvider/TryJoinGame")]
        void TryJoinGame();
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IServiceProvider/TryJoinGame")]
        System.Threading.Tasks.Task TryJoinGameAsync();
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IServiceProvider/LeaveGame")]
        void LeaveGame(System.Guid gameToken);
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IServiceProvider/LeaveGame")]
        System.Threading.Tasks.Task LeaveGameAsync(System.Guid gameToken);
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface IServiceProviderCallback {
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IServiceProvider/StartTurn")]
        void StartTurn(System.Guid token);
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IServiceProvider/ConfirmGameJoined")]
        void ConfirmGameJoined(System.Guid gameToken);
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IServiceProvider/GameInitialization")]
        void GameInitialization();
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface IServiceProviderChannel : Jabberwocky.SoC.Client.ServiceReference.IServiceProvider, System.ServiceModel.IClientChannel {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public partial class ServiceProviderClient : System.ServiceModel.DuplexClientBase<Jabberwocky.SoC.Client.ServiceReference.IServiceProvider>, Jabberwocky.SoC.Client.ServiceReference.IServiceProvider {
        
        public ServiceProviderClient(System.ServiceModel.InstanceContext callbackInstance) : 
                base(callbackInstance) {
        }
        
        public ServiceProviderClient(System.ServiceModel.InstanceContext callbackInstance, string endpointConfigurationName) : 
                base(callbackInstance, endpointConfigurationName) {
        }
        
        public ServiceProviderClient(System.ServiceModel.InstanceContext callbackInstance, string endpointConfigurationName, string remoteAddress) : 
                base(callbackInstance, endpointConfigurationName, remoteAddress) {
        }
        
        public ServiceProviderClient(System.ServiceModel.InstanceContext callbackInstance, string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(callbackInstance, endpointConfigurationName, remoteAddress) {
        }
        
        public ServiceProviderClient(System.ServiceModel.InstanceContext callbackInstance, System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(callbackInstance, binding, remoteAddress) {
        }
        
        public void TryJoinGame() {
            base.Channel.TryJoinGame();
        }
        
        public System.Threading.Tasks.Task TryJoinGameAsync() {
            return base.Channel.TryJoinGameAsync();
        }
        
        public void LeaveGame(System.Guid gameToken) {
            base.Channel.LeaveGame(gameToken);
        }
        
        public System.Threading.Tasks.Task LeaveGameAsync(System.Guid gameToken) {
            return base.Channel.LeaveGameAsync(gameToken);
        }
    }
}
