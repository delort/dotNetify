﻿/* 
Copyright 2016-2017 Dicky Suryadi

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DotNetify.Security;

namespace DotNetify
{
   /// <summary>
   /// This class is a SignalR hub for communicating with browser clients.
   /// </summary>
   public class DotNetifyHub : Hub
   {
      private readonly IVMControllerFactory _vmControllerFactory;
      private readonly IPrincipalAccessor _principalAccessor;
      private readonly IList<Func<IMiddleware>> _middlewareFactories;

      internal const string JTOKEN_vmArg = "$vmArg";
      internal const string JTOKEN_headers = "$headers";

      /// <summary>
      /// View model controller associated with the current connection.
      /// </summary>
      private VMController VMController
      {
         get
         {
            if (_principalAccessor is HubPrincipalAccessor)
               (_principalAccessor as HubPrincipalAccessor).Principal = Context.User;

            var vmController = _vmControllerFactory.GetInstance(Context.ConnectionId);
            vmController.Principal = _principalAccessor.Principal;
            return vmController;
         }
      }

      /// <summary>
      /// Constructor for dependency injection.
      /// </summary>
      /// <param name="vmControllerFactory">Factory of view model controllers.</param>
      /// <param name="principalAccessor">Allow to pass the hub principal.</param>
      /// <param name="middlewareFactories">Middlewares to intercept incoming view model requests and updates.</param>
      public DotNetifyHub(IVMControllerFactory vmControllerFactory, IPrincipalAccessor principalAccessor, IList<Func<IMiddleware>> middlewareFactories)
      {
         _vmControllerFactory = vmControllerFactory;
         _vmControllerFactory.ResponseDelegate = Response_VM;

         _principalAccessor = principalAccessor;
         _middlewareFactories = middlewareFactories;
      }

      /// <summary>
      /// Handles when a client gets disconnected.
      /// </summary>
      /// <param name="stopCalled">True, if stop was called on the client closing the connection gracefully;
      /// false, if the connection has been lost for longer than the timeout.</param>
      /// <returns></returns>
      public override Task OnDisconnected(bool stopCalled)
      {
         // Remove the controller on disconnection.
         _vmControllerFactory.Remove(Context.ConnectionId);
         return base.OnDisconnected(stopCalled);
      }

      #region Client Requests

      /// <summary>
      /// This method is called by browser clients to request view model data.
      /// </summary>
      /// <param name="vmId">Identifies the view model.</param>
      /// <param name="vmArg">Optional argument that may contain view model's initialization argument and/or request headers.</param>
      public void Request_VM(string vmId, object vmArg)
      {
         // Extract any header data out of the argument.
         object headers = null;
         if ( vmArg is JObject && (vmArg as JObject)[JTOKEN_vmArg] != null)
         {
            headers = (vmArg as JObject)[JTOKEN_headers];
            vmArg = (vmArg as JObject)[JTOKEN_vmArg];
         }

         try
         {
            RunMiddlewares(new DotNetifyHubContext(Context, nameof(Request_VM), vmId, vmArg));

            Trace.WriteLine($"[dotNetify] Request_VM: {vmId} {Context.ConnectionId}");
            VMController.OnRequestVM(Context.ConnectionId, vmId, vmArg);
         }
         catch (OperationCanceledException ex)
         {
            Trace.WriteLine(ex.Message);
         }
         catch (UnauthorizedAccessException ex)
         {
            Response_VM(Context.ConnectionId, vmId, SerializeException(ex));
         }
         catch (Exception ex)
         {
            Trace.Fail(ex.ToString());
         }
      }

      /// <summary>
      /// This method is called by browser clients to update a view model's value.
      /// </summary>
      /// <param name="vmId">Identifies the view model.</param>
      /// <param name="vmData">View model update data, where key is the property path and value is the property's new value.</param>
      public void Update_VM(string vmId, Dictionary<string, object> vmData)
      {
         try
         {
            RunMiddlewares(new DotNetifyHubContext(Context, nameof(Update_VM), vmId, vmData));

            Trace.WriteLine($"[dotNetify] Update_VM: {vmId} {Context.ConnectionId} {JsonConvert.SerializeObject(vmData)}");
            VMController.OnUpdateVM(Context.ConnectionId, vmId, vmData);
         }
         catch(OperationCanceledException ex)
         {
            Trace.WriteLine(ex.Message);
         }
         catch (UnauthorizedAccessException ex)
         {
            Response_VM(Context.ConnectionId, vmId, SerializeException(ex));
         }
         catch (Exception ex)
         {
            Trace.Fail(ex.ToString());
         }
      }

      /// <summary>
      /// This method is called by browser clients to remove its view model as it's no longer used.
      /// </summary>
      /// <param name="vmId">Identifies the view model.  By convention, this should match a view model class name.</param>
      public void Dispose_VM(string vmId)
      {
         try
         {
            VMController.OnDisposeVM(Context.ConnectionId, vmId);
         }
         catch (Exception ex)
         {
            Trace.Fail(ex.ToString());
         }
      }

      /// <summary>
      /// Run the middlewares.
      /// </summary>
      /// <param name="hubContext">DotNetify hub context.</param>
      private void RunMiddlewares(IDotNetifyHubContext hubContext)
      {
         try
         {
            _middlewareFactories?.ToList().ForEach(factory => factory().Invoke(hubContext));
         }
         catch (Exception ex)
         {
            Response_VM(Context.ConnectionId, hubContext.VMId, SerializeException(ex));
            throw new OperationCanceledException($"Middleware threw {ex.GetType().Name}: {ex.Message}", ex);
         }
      }

      /// <summary>
      /// Serializes an exception.
      /// </summary>
      /// <param name="ex">Exception to serialize.</param>
      /// <returns>Serialized exception.</returns>
      private string SerializeException(Exception ex) => JsonConvert.SerializeObject(new { ExceptionType = ex.GetType().Name, Message = ex.Message });

      #endregion

      #region Server Responses

      /// <summary>
      /// This method is called by the VMManager to send response back to browser clients.
      /// </summary>
      /// <param name="connectionId">Identifies the browser client making prior request.</param>
      /// <param name="vmId">Identifies the view model.</param>
      /// <param name="vmData">View model data in serialized JSON.</param>
      public void Response_VM(string connectionId, string vmId, string vmData)
      {
         Trace.WriteLine($"[dotNetify] Response_VM: {vmId} {connectionId} {vmData}");
         if (_vmControllerFactory.GetInstance(connectionId) != null) // Touch the factory to push the timeout.
            Clients.Client(connectionId).Response_VM(vmId, vmData);
      }

      #endregion
   }
}