﻿/* 
Copyright 2016 Dicky Suryadi

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

namespace DotNetify
{
   /// <summary>
   /// Provides view model controllers.
   /// </summary>
   public interface IVMControllerFactory
   {
      /// <summary>
      /// Creates a view model controller and assigns it a key. 
      /// On subsequent calls, use the same key to return the same object.
      /// </summary>
      /// <param name="key">Identifies the object.</param>
      /// <returns>View model controller.</returns>
      VMController GetInstance(string key);

      /// <summary>
      /// Removes an existing view model controller.
      /// </summary>
      /// <param name="key">Identifies the object.</param>
      /// <returns>True if the object was removed.</returns>
      bool Remove(string key);
   }
}
