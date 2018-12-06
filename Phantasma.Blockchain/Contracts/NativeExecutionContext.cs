﻿using Phantasma.Core;
using Phantasma.VM;
using Phantasma.VM.Contracts;
using System;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Contracts
{
    public class NativeExecutionContext : ExecutionContext
    {
        public readonly SmartContract Contract;

        public NativeExecutionContext(SmartContract contract)
        {
            this.Contract = contract;
        }

        public override ExecutionState Execute(ExecutionFrame frame, Stack<VMObject> stack)
        {
            if (this.Contract.ABI == null)
            {
#if DEBUG
                throw new VMDebugException(frame.VM, $"VM nativecall failed: ABI is missing for contract '{this.Contract.Name}'");
#endif
                return ExecutionState.Fault;
            }

            if (stack.Count <= 0)
            {
#if DEBUG
                throw new VMDebugException(frame.VM, $"VM nativecall failed: method name not present in the VM stack");
#endif
                return ExecutionState.Fault;
            }

            var stackObj = stack.Pop();
            var methodName = stackObj.AsString();
            var method = this.Contract.ABI.FindMethod(methodName);

            if (method == null)
            {
#if DEBUG
                throw new VMDebugException(frame.VM, $"VM nativecall failed: contract '{this.Contract.Name}' does not have method '{methodName}' in its ABI");
#endif
                return ExecutionState.Fault;
            }

            if (stack.Count < method.parameters.Length)
            {
#if DEBUG
                throw new VMDebugException(frame.VM, $"VM nativecall failed: calling method {methodName} with {stack.Count} arguments instead of {method.parameters.Length}");
#endif
                return ExecutionState.Fault;
            }

            if (this.Contract.HasInternalMethod(methodName))
            {
                ExecutionState result;
                try
                {
                    result = InternalCall(method, frame, stack);
                }
                catch (ArgumentException ex)
                {
#if DEBUG
                    throw new VMDebugException(frame.VM, $"VM nativecall failed: calling method {methodName} with arguments of wrong type, "+ex.ToString());
#endif
                }
                return result;
            }

            var customContract = this.Contract as CustomContract;

            if (customContract == null)
            {
#if DEBUG
                throw new VMDebugException(frame.VM, $"VM nativecall failed: contract '{this.Contract.Name}' is not a valid custom contract");
#endif
                return ExecutionState.Fault;
            }

            stack.Push(stackObj);

            var context = new ScriptContext(customContract.Script);
            return context.Execute(frame, stack);
        }

        private ExecutionState InternalCall(ContractMethod method, ExecutionFrame frame, Stack<VMObject> stack)
        {
            var args = new object[method.parameters.Length];
            for (int i = 0; i < args.Length; i++)
            {
                var arg = stack.Pop();

                var temp = arg.Data;

                // when a string is passed instead of an address we do an automatic lookup and replace
                if (method.parameters[i] == VMType.Object && temp is string)
                {
                    var name = (string)temp;
                    var runtime = (RuntimeVM)frame.VM;
                    var address = runtime.Nexus.LookUpName(name);
                    temp = address;
                }

                args[i] = temp;
            }

            var result = this.Contract.CallInternalMethod(method.name, args);

            if (method.returnType != VMType.None)
            {
                var obj = VMObject.FromObject(result);
                stack.Push(obj);
            }

            return ExecutionState.Running;
        }

        public override int GetSize()
        {
            return 0;
        }
    }
}
