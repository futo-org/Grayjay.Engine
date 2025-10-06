using Grayjay.Engine.V8;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Grayjay.Engine
{
    public class GrayjayTestSystem
    {
        private GrayjayPlugin _plugin;
        private DescriptorState _descriptor;
        private Queue<(TestState, TestContext)> _queue = new Queue<(TestState, TestContext)>();

        private object _threadLock = new object();
        private Thread _thread = null;
        

        public GrayjayTestSystem(GrayjayPlugin plugin, DescriptorState descriptor = null)
        {
            _plugin = plugin;
            _descriptor = descriptor;
            if(descriptor == null)
                _descriptor = EvaluateDescriptor(plugin);
        }


        public void RunTestQueueSingleton()
        {
            lock (_threadLock)
            {
                if (_thread == null)
                {
                    _thread = new Thread(() =>
                    {
                        try
                        {
                            while (_queue.Count > 0)
                            {
                                TestState state = null;
                                TestContext context = null;
                                lock (_queue)
                                {
                                    (state, context) = _queue.Dequeue();
                                }
                                if (state != null && context != null)
                                {

                                    try
                                    {
                                        TestState testState = RunTest(state, context);
                                        if (context.CompletionSource != null)
                                            context.CompletionSource.SetResult(testState);
                                    }
                                    catch (Exception ex)
                                    {
                                        context.CompletionSource.SetException(ex);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error<GrayjayTestSystem>("Main test system loop failed", ex);
                        }
                        finally
                        {
                            lock (_threadLock)
                            {
                                _thread = null;
                            }
                        }
                    });
                    _thread.Start();
                }
            }
        }

        public Task<TestState> QueueTestAsync(string variable, Dictionary<string, object> context, bool isolated = true)
        {
            var test = _descriptor.Tests.FirstOrDefault(x => x.Test.Variable == variable);
            if (test == null)
                throw new NotImplementedException($"No implemented test with variable {variable}");


            if (test.Status == StatusType.Running || test.Status == StatusType.Queued)
                throw new Exception("Already started");
            test.Status = StatusType.Queued;

            TaskCompletionSource<TestState> source = new TaskCompletionSource<TestState>();

            var testContext = new TestContext(_plugin, isolated, source)
            {
                Caller = test.Test.Variable,
                Metadata = context?.ToPropertyBag() ?? new PropertyBag()
            };

            lock (_queue)
            {
                _queue.Enqueue((test, testContext));
            }
            RunTestQueueSingleton();


            return source.Task;
        }

        public TestState RunTest(string variable, Dictionary<string, object> context, bool isolated = true)
        {

            var test = _descriptor.Tests.FirstOrDefault(x => x.Test.Variable == variable);
            if (test == null)
                throw new NotImplementedException($"No implemented test with variable {variable}");

            var testContext = new TestContext(_plugin)
            {
                Isolated = isolated,
                Metadata = context?.ToPropertyBag() ?? new PropertyBag()
            };

            return RunTest(test, testContext);
        }
        public TestState RunTest(TestState test, TestContext context)
        {
            test.Result = null;
            test.Logs = new List<string>();
            object result = false;
            Stopwatch watch = new Stopwatch();
            List<string> logs = new List<string>();
            var logHandler = (PluginConfig config, string log) =>
            {
                logs.Add($"[{(int)watch.Elapsed.TotalMilliseconds}ms]: {log}");
                test.Logs = logs;
            };

            Exception enableException = null;

            var plugin = context.Isolated ? _plugin.GetCopy(false, new GrayjayPlugin.Options()
            {
                CaseInsensitive = true,
                IncludeStandardTests = true
            }) : _plugin;
            if (!plugin.IsEnabled)
            {
                try
                {
                    plugin.Initialize();
                    plugin.Enable();
                }
                catch(Exception ex)
                {
                    enableException = ex;
                }
            }
            plugin.OnLog += logHandler;
            try
            {
                if (enableException != null)
                    throw new Exception($"Enable failed: " + enableException.Message, enableException);

                var nativeRef = plugin.EvaluateRawObject("GrayjayTests");
                if (test.Test.Implicit)
                {
                    test.Status = StatusType.Running;
                    watch.Start();
                    result = nativeRef.InvokeV8(plugin.Config, test.Test.Variable, context);
                    watch.Stop();
                }
                else
                {
                    test.Status = StatusType.Running;
                    var subObject = nativeRef.GetOrThrow<IJavaScriptObject>(plugin, test.Test.Variable, "TestSystem", false);
                    watch.Start();
                    result = subObject.InvokeV8(plugin.Config, "test", context);
                    watch.Stop();
                }
                if (result is IJavaScriptObject)
                    result = JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch(Exception ex)
            {
                test.Status = StatusType.Failure;
                test.Exception = ex.Message;
                test.Result = null;
                test.Logs = logs;
                return new TestState(test.Test)
                {
                    Status = StatusType.Failure,
                    Exception = ex.Message,
                    Time = (int)watch.Elapsed.TotalMilliseconds,
                    Result = null,
                    Logs = logs
                };
            }
            finally
            {
                plugin.OnLog -= logHandler;
                if(context.Isolated)
                {
                    try
                    {
                        plugin.Disable();
                    }
                    catch(Exception ex)
                    {
                        Logger.Error<GrayjayTestSystem>("Failed to dispose isolated plugin");
                    }
                }
            }

            test.Status = StatusType.Success;
            test.Result = result;
            test.Time = (int)watch.Elapsed.TotalMilliseconds;
            test.Logs = logs;
            return new TestState(test.Test)
            {
                Status = StatusType.Success,
                Time = (int)watch.Elapsed.TotalMilliseconds,
                Result = result,
                Logs = logs
            };
        }



        public DescriptorState GetDescriptorState()
        {
            return _descriptor;
        }


        public static DescriptorState EvaluateDescriptor(GrayjayPlugin plugin)
        {
            IJavaScriptObject descriptorObj = null;
            try
            {
                descriptorObj = plugin.EvaluateRawObject("GrayjayTests");
            }
            catch(Exception ex)
            {
                Logger.Error<GrayjayTestSystem>("Failed to retrieve GrayjayTests");
            }
            if (descriptorObj == null)
                return new DescriptorState()
                {
                    Tests = new List<TestState>()
                };
            else
            {
                var tests = new List<TestState>();
                var descriptor = new DescriptorState()
                {
                    Tests = tests
                };
                foreach (var key in descriptorObj.PropertyNames)
                {
                    var testObj = descriptorObj.GetOrThrow<IJavaScriptObject>(plugin, key, "Descriptor", false);
                    if (testObj.Kind == JavaScriptObjectKind.Function)
                    {
                        var func = testObj;
                        var name = key;
                        var description = "";
                        var test = new TestDescriptor()
                        {
                            Name = name ?? key,
                            Description = description ?? "",
                            Variable = key,
                            Required = new string[0],
                            Implicit = true
                        };
                        tests.Add(new TestState(test));
                    }
                    else
                    {
                        //var func = testObj.Test;
                        var name = testObj.GetOrDefault<string>(plugin, "name", "Descriptor.Name", key);
                        var description = testObj.GetOrDefault<string>(plugin, "description", "Descriptor.Description", "No description");
                        var requirements = testObj.GetOrDefault<string[]>(plugin, "required", "Descriptor.Description", new string[0]);
                        var test = new TestDescriptor()
                        {
                            Name = name ?? key,
                            Description = description ?? "",
                            Required = requirements,
                            Variable = key
                        };

                        bool valid = true;
                        if (requirements.Length > 0)
                        {
                            if (!plugin.Config.Testing.ContainsKey(key))
                            {
                                var functionMetaRaw = plugin.Config.Testing[key];

                                Dictionary<string, string> functionMeta = (Dictionary<string, string>)functionMetaRaw;

                                foreach (var requirement in requirements)
                                {
                                    if (requirement == "DEFINED")
                                        continue;

                                    if (functionMeta.ContainsKey(requirement))
                                        continue;
                                    else
                                    {
                                        valid = false;
                                        break;
                                    }
                                }
                            }
                            else
                                valid = false;
                        }

                        if(valid)
                            tests.Add(new TestState(test));
                    }
                }

                return descriptor;
            }
        }

        public class TestDescriptor
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string[] Required { get; set; }
            public string Variable { get; set; }
            public bool Implicit { get; set; }
        }
        public class DescriptorState
        {
            public List<TestState> Tests { get; set; }
        }
        public class Descriptor
        {
            public List<TestDescriptor> Tests { get; set; }
        }

        public class TestState
        {
            public TestDescriptor Test { get; set; }
            public string Message { get; set; }
            public StatusType Status { get; set; }
            public string Exception { get; set; }
            public object Result { get; set; }
            public int Time { get; set; }
            public List<string> Logs { get; set; }

            public TestState(TestDescriptor descriptor)
            {
                Test = descriptor;
                Message = "";
                Status = StatusType.Unknown;
                Exception = null;
                Result = null;
            }
        }
        public enum StatusType: int
        {
            Unknown = 0,
            Queued = 1,
            Running = 2,
            Success = 3,
            Failure = 4
        }


        [DefaultScriptUsage(ScriptAccess.Full)]
        public class TestContext
        {
            private GrayjayPlugin _plugin;

            [ScriptMember("isolated")]
            public bool Isolated { get; set; } = true;

            [ScriptMember("caller")]
            public string Caller { get; set; }
            [ScriptMember("implicit")]
            public bool Implicit { get; set; }


            [ScriptMember("metadata")]
            public object Metadata { get; set; } = new object();


            public TaskCompletionSource<TestState> CompletionSource { get; set; }



            public TestContext(GrayjayPlugin plugin, bool isolated = true, TaskCompletionSource<TestState> completion = null)
            {
                _plugin = plugin;
                Isolated = isolated;
                CompletionSource = completion;
            }


            [ScriptMember("runSourceMethod", ScriptMemberFlags.ExposeRuntimeType)]
            public object RunSourceMethod(string method, IJavaScriptObject paraObj)
            {
                var paras = V8Converter.ConvertValue<object[]>(_plugin, paraObj);

                (GrayjayPlugin.JSCallDocs doc, MethodInfo methodInfo) = GrayjayPlugin.GetJSDocsMethods().FirstOrDefault(x => x.Item1.Title == method);
                if (doc == null || methodInfo == null)
                    throw new NotImplementedException($"Source Method [{method}] does not exist");

                var nativeParameters = methodInfo.GetParameters();
                List<object> parasNative = new List<object>();
                for(int i = 0; i < nativeParameters.Length; i++)
                {
                    var nativeParameter = nativeParameters[i];
                    var paraNativeVal = V8Converter.ConvertValue(_plugin, nativeParameters[i].ParameterType, paras[i]);
                    parasNative.Add(paraNativeVal);
                }

                var result = methodInfo.Invoke(_plugin, parasNative.ToArray());
                return result;
            }
        }
    }
}
