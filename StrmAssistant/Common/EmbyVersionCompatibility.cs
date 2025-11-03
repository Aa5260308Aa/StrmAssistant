using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace StrmAssistant.Common
{
    /// <summary>
    /// Emby版本兼容性检测工具类
    /// 用于检测当前Emby版本与插件功能的兼容性
    /// </summary>
    public static class EmbyVersionCompatibility
    {
        // 已知的Emby版本里程碑
        public static readonly Version Version4800 = new Version("4.8.0.0");
        public static readonly Version Version4830 = new Version("4.8.3.0");
        public static readonly Version Version4900 = new Version("4.9.0.0");
        public static readonly Version Version4910 = new Version("4.9.1.0");
        public static readonly Version Version49180 = new Version("4.9.1.80");
        
        private static Version _currentVersion;
        
        public static Version CurrentVersion
        {
            get
            {
                if (_currentVersion == null)
                {
                    _currentVersion = Plugin.Instance.ApplicationHost.ApplicationVersion;
                }
                return _currentVersion;
            }
        }

        /// <summary>
        /// 检查方法签名是否匹配
        /// </summary>
        public static bool CheckMethodSignature(MethodInfo method, Type[] expectedParameterTypes)
        {
            if (method == null) return false;
            
            var parameters = method.GetParameters();
            if (parameters.Length != expectedParameterTypes.Length) return false;
            
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType != expectedParameterTypes[i])
                {
                    return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// 尝试查找兼容的方法重载
        /// 支持多个版本的方法签名
        /// </summary>
        public static MethodInfo FindCompatibleMethod(Type type, string methodName, 
            BindingFlags bindingFlags, params Type[][] parameterTypeVariants)
        {
            if (type == null || string.IsNullOrEmpty(methodName))
                return null;

            foreach (var parameterTypes in parameterTypeVariants)
            {
                try
                {
                    var method = type.GetMethod(methodName, bindingFlags, null, parameterTypes, null);
                    if (method != null)
                    {
                        if (Plugin.Instance.DebugMode)
                        {
                            Plugin.Instance.Logger.Debug(
                                $"Found compatible method: {type.Name}.{methodName} with {parameterTypes.Length} parameters");
                        }
                        return method;
                    }
                }
                catch (AmbiguousMatchException)
                {
                    // 如果有歧义，尝试更精确的匹配
                    var methods = type.GetMethods(bindingFlags)
                        .Where(m => m.Name == methodName && CheckMethodSignature(m, parameterTypes))
                        .ToArray();
                    
                    if (methods.Length == 1)
                    {
                        return methods[0];
                    }
                }
                catch (Exception ex)
                {
                    if (Plugin.Instance.DebugMode)
                    {
                        Plugin.Instance.Logger.Debug($"Method lookup failed for {methodName}: {ex.Message}");
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 获取方法的安全调用包装器
        /// 提供异常处理和日志记录
        /// </summary>
        public static Func<object, object[], object> CreateSafeMethodInvoker(MethodInfo method, string contextName)
        {
            if (method == null)
            {
                Plugin.Instance.Logger.Warn($"Cannot create invoker for null method in {contextName}");
                return null;
            }

            return (instance, args) =>
            {
                try
                {
                    return method.Invoke(instance, args);
                }
                catch (TargetInvocationException tie)
                {
                    var innerEx = tie.InnerException ?? tie;
                    Plugin.Instance.Logger.Error($"Method invocation failed in {contextName}: {innerEx.Message}");
                    if (Plugin.Instance.DebugMode)
                    {
                        Plugin.Instance.Logger.Debug($"Method: {method.DeclaringType?.Name}.{method.Name}");
                        Plugin.Instance.Logger.Debug($"Arguments: {string.Join(", ", args?.Select(a => a?.GetType().Name ?? "null") ?? new[] { "none" })}");
                        Plugin.Instance.Logger.Debug(innerEx.StackTrace);
                    }
                    throw innerEx;
                }
                catch (Exception ex)
                {
                    Plugin.Instance.Logger.Error($"Unexpected error in {contextName}: {ex.Message}");
                    if (Plugin.Instance.DebugMode)
                    {
                        Plugin.Instance.Logger.Debug(ex.StackTrace);
                    }
                    throw;
                }
            };
        }

        /// <summary>
        /// 检查类型是否存在指定的成员
        /// </summary>
        public static bool HasMember(Type type, string memberName, MemberTypes memberType)
        {
            if (type == null || string.IsNullOrEmpty(memberName))
                return false;

            try
            {
                var members = type.GetMember(memberName, memberType, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                return members.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取属性的安全访问器
        /// </summary>
        public static Func<object, object> CreateSafePropertyGetter(PropertyInfo property, string contextName)
        {
            if (property == null || !property.CanRead)
            {
                Plugin.Instance.Logger.Warn($"Cannot create getter for property in {contextName}");
                return null;
            }

            var getter = property.GetGetMethod(true);
            if (getter == null) return null;

            return (instance) =>
            {
                try
                {
                    return getter.Invoke(instance, null);
                }
                catch (Exception ex)
                {
                    Plugin.Instance.Logger.Error($"Property getter failed in {contextName}: {ex.Message}");
                    if (Plugin.Instance.DebugMode)
                    {
                        Plugin.Instance.Logger.Debug($"Property: {property.DeclaringType?.Name}.{property.Name}");
                        Plugin.Instance.Logger.Debug(ex.StackTrace);
                    }
                    throw;
                }
            };
        }

        /// <summary>
        /// 获取属性的安全设置器
        /// </summary>
        public static Action<object, object> CreateSafePropertySetter(PropertyInfo property, string contextName)
        {
            if (property == null || !property.CanWrite)
            {
                Plugin.Instance.Logger.Warn($"Cannot create setter for property in {contextName}");
                return null;
            }

            var setter = property.GetSetMethod(true);
            if (setter == null) return null;

            return (instance, value) =>
            {
                try
                {
                    setter.Invoke(instance, new[] { value });
                }
                catch (Exception ex)
                {
                    Plugin.Instance.Logger.Error($"Property setter failed in {contextName}: {ex.Message}");
                    if (Plugin.Instance.DebugMode)
                    {
                        Plugin.Instance.Logger.Debug($"Property: {property.DeclaringType?.Name}.{property.Name}");
                        Plugin.Instance.Logger.Debug($"Value type: {value?.GetType().Name ?? "null"}");
                        Plugin.Instance.Logger.Debug(ex.StackTrace);
                    }
                    throw;
                }
            };
        }

        /// <summary>
        /// 记录版本兼容性诊断信息
        /// </summary>
        public static void LogCompatibilityInfo(string componentName, bool isCompatible, string details = null)
        {
            var status = isCompatible ? "✓ Compatible" : "✗ Incompatible";
            var message = $"[{componentName}] {status} with Emby {CurrentVersion}";
            
            if (!string.IsNullOrEmpty(details))
            {
                message += $" - {details}";
            }

            if (isCompatible)
            {
                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug(message);
                }
            }
            else
            {
                Plugin.Instance.Logger.Warn(message);
            }
        }

        /// <summary>
        /// 尝试加载程序集
        /// </summary>
        public static Assembly TryLoadAssembly(string assemblyName)
        {
            try
            {
                return Assembly.Load(assemblyName);
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Warn($"Failed to load assembly '{assemblyName}': {ex.Message}");
                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug(ex.StackTrace);
                }
                return null;
            }
        }

        /// <summary>
        /// 尝试获取类型
        /// </summary>
        public static Type TryGetType(Assembly assembly, string typeName)
        {
            if (assembly == null) return null;

            try
            {
                return assembly.GetType(typeName);
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Warn($"Failed to get type '{typeName}' from assembly '{assembly.GetName().Name}': {ex.Message}");
                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug(ex.StackTrace);
                }
                return null;
            }
        }
    }
}

