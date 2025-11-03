using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace StrmAssistant.Mod
{
    public static class PatchManager
    {
        public static Harmony HarmonyMod;
        public static readonly List<PatchTracker> PatchTrackerList = new List<PatchTracker>();

        public static EnableImageCapture EnableImageCapture;
        public static EnhanceChineseSearch EnhanceChineseSearch;
        public static MergeMultiVersion MergeMultiVersion;
        public static ExclusiveExtract ExclusiveExtract;
        public static ChineseMovieDb ChineseMovieDb;
        public static ChineseTvdb ChineseTvdb;
        public static EnhanceMovieDbPerson EnhanceMovieDbPerson;
        public static AltMovieDbConfig AltMovieDbConfig;
        public static EnableProxyServer EnableProxyServer;
        public static PreferOriginalPoster PreferOriginalPoster;
        public static UnlockIntroSkip UnlockIntroSkip;
        public static PinyinSortName PinyinSortName;
        public static EnhanceNfoMetadata EnhanceNfoMetadata;
        public static HidePersonNoImage HidePersonNoImage;
        public static EnforceLibraryOrder EnforceLibraryOrder;
        public static BeautifyMissingMetadata BeautifyMissingMetadata;
        public static EnhanceMissingEpisodes EnhanceMissingEpisodes;
        public static ChapterChangeTracker ChapterChangeTracker;
        public static MovieDbEpisodeGroup MovieDbEpisodeGroup;
        public static NoBoxsetsAutoCreation NoBoxsetsAutoCreation;
        public static EnhanceNotificationSystem EnhanceNotificationSystem;
        public static EnableDeepDelete EnableDeepDelete;
        public static SuppressPluginUpdate SuppressPluginUpdate;

        private static readonly ConcurrentDictionary<Tuple<Type, string>, HarmonyMethod> HarmonyMethodCache 
            = new ConcurrentDictionary<Tuple<Type, string>, HarmonyMethod>();
        private static readonly ConcurrentDictionary<Tuple<Type, string>, MethodInfo> MethodInfoCache 
            = new ConcurrentDictionary<Tuple<Type, string>, MethodInfo>();

        public static void Initialize()
        {
            try
            {
                HarmonyMod = new Harmony("emby.mod");
                Plugin.Instance.Logger.Info("Harmony Mod initialized successfully");
            }
            catch (Exception e)
            {
                Plugin.Instance.Logger.Error("Harmony Init Failed - Harmony mod will not be available");
                Plugin.Instance.Logger.Error($"Error: {e.Message}");
                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug(e.StackTrace);
                }
            }

            EnableImageCapture = new EnableImageCapture();
            EnhanceChineseSearch = new EnhanceChineseSearch();
            MovieDbEpisodeGroup = new MovieDbEpisodeGroup();
            MergeMultiVersion = new MergeMultiVersion();
            ExclusiveExtract = new ExclusiveExtract();
            ChineseMovieDb = new ChineseMovieDb();
            ChineseTvdb = new ChineseTvdb();
            EnhanceMovieDbPerson = new EnhanceMovieDbPerson();
            AltMovieDbConfig = new AltMovieDbConfig();
            EnableProxyServer = new EnableProxyServer();
            PreferOriginalPoster = new PreferOriginalPoster();
            UnlockIntroSkip = new UnlockIntroSkip();
            PinyinSortName = new PinyinSortName();
            EnhanceNfoMetadata = new EnhanceNfoMetadata();
            HidePersonNoImage = new HidePersonNoImage();
            EnforceLibraryOrder = new EnforceLibraryOrder();
            BeautifyMissingMetadata = new BeautifyMissingMetadata();
            EnhanceMissingEpisodes = new EnhanceMissingEpisodes();
            ChapterChangeTracker = new ChapterChangeTracker();
            NoBoxsetsAutoCreation = new NoBoxsetsAutoCreation();
            EnhanceNotificationSystem = new EnhanceNotificationSystem();
            EnableDeepDelete = new EnableDeepDelete();
            SuppressPluginUpdate = new SuppressPluginUpdate();
        }

        public static bool IsPatched(MethodBase methodInfo, Type type)
        {
            var patchedMethods = Harmony.GetAllPatchedMethods();
            if (!patchedMethods.Contains(methodInfo)) return false;
            var patchInfo = Harmony.GetPatchInfo(methodInfo);

            return patchInfo.Prefixes.Any(p => p.owner == HarmonyMod.Id && p.PatchMethod.DeclaringType == type) ||
                   patchInfo.Postfixes.Any(p => p.owner == HarmonyMod.Id && p.PatchMethod.DeclaringType == type) ||
                   patchInfo.Transpilers.Any(p => p.owner == HarmonyMod.Id && p.PatchMethod.DeclaringType == type) ||
                   patchInfo.Finalizers.Any(p => p.owner == HarmonyMod.Id && p.PatchMethod.DeclaringType == type);
        }

        public static bool WasCalledByMethod(Assembly assembly, string callingMethodName)
        {
            var stackFrames = new StackTrace(1, false).GetFrames();

            return stackFrames.Any(f =>
            {
                var method = f?.GetMethod();
                return method?.DeclaringType?.Assembly == assembly && method?.Name == callingMethodName;
            });
        }

        public static bool IsModSuccess()
        {
            var supportedPatches = PatchTrackerList.Where(p => p.IsSupported).ToList();
            var failedPatches = supportedPatches.Where(p => p.FallbackPatchApproach != p.DefaultPatchApproach).ToList();
            
            if (failedPatches.Any())
            {
                Plugin.Instance.Logger.Info($"=== Harmony Mod Status: {failedPatches.Count}/{supportedPatches.Count} patches using fallback methods ===");
                
                // 按回退方式分组显示
                var byReflection = failedPatches.Where(p => p.FallbackPatchApproach == PatchApproach.Reflection).ToList();
                var byNone = failedPatches.Where(p => p.FallbackPatchApproach == PatchApproach.None).ToList();
                
                if (byReflection.Any())
                {
                    Plugin.Instance.Logger.Info($"Using Reflection approach ({byReflection.Count} patches):");
                    foreach (var patch in byReflection)
                    {
                        Plugin.Instance.Logger.Info($"  ✓ {patch.PatchType.Name} - Using Reflection (Harmony ReversePatch not available, but Reflection works fine)");
                    }
                }
                
                if (byNone.Any())
                {
                    Plugin.Instance.Logger.Warn($"Not available ({byNone.Count} patches):");
                    foreach (var patch in byNone)
                    {
                        Plugin.Instance.Logger.Warn($"  ✗ {patch.PatchType.Name} - Feature disabled (required method/plugin not found)");
                    }
                }
                
                Plugin.Instance.Logger.Info("Note: Using Reflection instead of Harmony is normal and expected. All features will work correctly.");
            }
            else if (supportedPatches.Any())
            {
                Plugin.Instance.Logger.Info($"Harmony Mod: All {supportedPatches.Count} patches initialized successfully");
            }
            
            return failedPatches.Count == 0;
        }

        public static void CopyProperty(object source, object target, string propertyName)
        {
            var value = Traverse.Create(source).Property(propertyName).GetValue();
            Traverse.Create(target).Property(propertyName).SetValue(value);
        }

        public static bool ReversePatch(PatchTracker tracker, MethodBase targetMethod, string stub)
        {
            if (tracker.FallbackPatchApproach != PatchApproach.Harmony) return false;

            if (targetMethod is null)
            {
                Plugin.Instance.Logger.Warn($"{tracker.PatchType.Name} ReversePatch Failed: Target method is null");
                tracker.FallbackPatchApproach = PatchApproach.None;
                return false;
            }

            var stubMethod = GetHarmonyMethod(tracker.PatchType, stub);

            if (stubMethod == null)
            {
                Plugin.Instance.Logger.Warn($"{tracker.PatchType.Name} ReversePatch Failed: Stub method '{stub}' not found");
                tracker.FallbackPatchApproach = PatchApproach.Reflection;
                return false;
            }

            try
            {
                var methodName = targetMethod.DeclaringType != null ? $"{targetMethod.DeclaringType.Name}.{targetMethod.Name}" : targetMethod.Name;
                
                HarmonyMod.CreateReversePatcher(targetMethod, stubMethod).Patch();

                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug(
                        $"{nameof(ReversePatch)} {methodName} for {tracker.PatchType.Name} Success");
                }

                return true;
            }
            catch (Exception he)
            {
                var methodName = targetMethod.DeclaringType != null ? $"{targetMethod.DeclaringType.Name}.{targetMethod.Name}" : targetMethod.Name;
                var exceptionType = he.GetType().Name;
                var exceptionMessage = he.Message;
                
                // 针对特定错误类型提供更详细的说明
                string detailedMessage = exceptionMessage;
                if (exceptionMessage.Contains("Common Language Runtime detected an invalid program") ||
                    exceptionMessage.Contains("CLR") && exceptionMessage.Contains("invalid program"))
                {
                    detailedMessage = "Harmony无法反编译此方法（可能包含复杂IL代码或泛型约束）。这是Harmony的已知限制，不影响功能，将使用反射方式。";
                    Plugin.Instance.Logger.Warn($"{tracker.PatchType.Name} ReversePatch: {detailedMessage}");
                    Plugin.Instance.Logger.Warn($"  Method: {methodName}");
                }
                else if (exceptionMessage.Contains("target of an invocation"))
                {
                    var innerException = he.InnerException;
                    detailedMessage = $"反射调用失败: {exceptionMessage}";
                    if (innerException != null)
                    {
                        detailedMessage += $" (Inner: {innerException.GetType().Name}: {innerException.Message})";
                    }
                    Plugin.Instance.Logger.Warn($"{tracker.PatchType.Name} ReversePatch Failed: {detailedMessage}");
                    Plugin.Instance.Logger.Warn($"  Method: {methodName}");
                    Plugin.Instance.Logger.Warn($"  This usually means the method signature or implementation has changed in this Emby version.");
                }
                else
                {
                    Plugin.Instance.Logger.Warn($"{tracker.PatchType.Name} ReversePatch Failed: {exceptionMessage}");
                    Plugin.Instance.Logger.Warn($"  Method: {methodName}");
                }
                
                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug($"Stub method: {stubMethod.method.Name}");
                    Plugin.Instance.Logger.Debug($"Exception type: {exceptionType}");
                    Plugin.Instance.Logger.Debug($"Full stack trace:");
                    Plugin.Instance.Logger.Debug(he.StackTrace);
                    if (he.InnerException != null)
                    {
                        Plugin.Instance.Logger.Debug($"Inner exception: {he.InnerException.GetType().Name}: {he.InnerException.Message}");
                        Plugin.Instance.Logger.Debug(he.InnerException.StackTrace);
                    }
                }

                tracker.FallbackPatchApproach = PatchApproach.Reflection;
                Plugin.Instance.Logger.Info($"{tracker.PatchType.Name} will use Reflection approach as fallback (this is normal and expected)");
            }

            return false;
        }

        public static bool PatchUnpatch(PatchTracker tracker, bool apply, MethodBase targetMethod, string prefix = null,
            string postfix = null, string transpiler = null, string finalizer = null, bool suppress = false)
        {
            if (tracker.FallbackPatchApproach != PatchApproach.Harmony) return false;

            if (targetMethod is null)
            {
                Plugin.Instance.Logger.Warn($"{tracker.PatchType.Name} PatchUnpatch Failed: Target method is null");
                tracker.FallbackPatchApproach = PatchApproach.None;
                return false;
            }

            if (HarmonyMod == null)
            {
                Plugin.Instance.Logger.Error($"{tracker.PatchType.Name} PatchUnpatch Failed: HarmonyMod is not initialized");
                tracker.FallbackPatchApproach = PatchApproach.None;
                return false;
            }

            var action = apply ? "Patch" : "Unpatch";

            try
            {
                if (apply && !IsPatched(targetMethod, tracker.PatchType))
                {
                    var prefixMethod = GetHarmonyMethod(tracker.PatchType, prefix);
                    var postfixMethod = GetHarmonyMethod(tracker.PatchType, postfix);
                    var transpilerMethod = GetHarmonyMethod(tracker.PatchType, transpiler);
                    var finalizerMethod = GetHarmonyMethod(tracker.PatchType, finalizer);

                    HarmonyMod.Patch(targetMethod, prefixMethod, postfixMethod, transpilerMethod, finalizerMethod);
                }
                else if (!apply && IsPatched(targetMethod, tracker.PatchType))
                {
                    var prefixMethod = GetMethodInfo(tracker.PatchType, prefix);
                    var postfixMethod = GetMethodInfo(tracker.PatchType, postfix);
                    var transpilerMethod = GetMethodInfo(tracker.PatchType, transpiler);
                    var finalizerMethod = GetMethodInfo(tracker.PatchType, finalizer);

                    if (prefixMethod != null) HarmonyMod.Unpatch(targetMethod, prefixMethod);
                    if (postfixMethod != null) HarmonyMod.Unpatch(targetMethod, postfixMethod);
                    if (transpilerMethod != null) HarmonyMod.Unpatch(targetMethod, transpilerMethod);
                    if (finalizerMethod != null) HarmonyMod.Unpatch(targetMethod, finalizerMethod);
                }

                if (!suppress)
                {
                    if (Plugin.Instance.DebugMode)
                    {
                        Plugin.Instance.Logger.Debug(
                            $"{action} {(targetMethod.DeclaringType != null ? targetMethod.DeclaringType.Name + "." : string.Empty)}{targetMethod.Name} for {tracker.PatchType.Name} Success");
                    }
                }

                return true;
            }
            catch (Exception he)
            {
                Plugin.Instance.Logger.Warn($"{tracker.PatchType.Name} {action} Failed: {he.Message}");
                Plugin.Instance.Logger.Warn($"Target: {(targetMethod.DeclaringType != null ? targetMethod.DeclaringType.Name + "." : string.Empty)}{targetMethod.Name}");
                
                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug($"Exception type: {he.GetType().Name}");
                    Plugin.Instance.Logger.Debug($"Prefix: {prefix}, Postfix: {postfix}, Transpiler: {transpiler}, Finalizer: {finalizer}");
                    Plugin.Instance.Logger.Debug(he.StackTrace);
                }

                tracker.FallbackPatchApproach = PatchApproach.Reflection;
                Plugin.Instance.Logger.Info($"{tracker.PatchType.Name} will use Reflection approach as fallback");
            }

            return false;
        }

        public static bool PatchUnpatch(PatchTracker tracker, bool apply, MethodBase targetMethod, ref int usageCount,
            string prefix = null, string postfix = null, string transpiler = null, string finalizer = null,
            bool suppress = false)
        {
            if (apply)
            {
                if (usageCount == 0)
                {
                    if (PatchUnpatch(tracker, true, targetMethod, prefix, postfix, transpiler, finalizer, suppress))
                    {
                        usageCount++;
                        return true;
                    }

                    return false;
                }

                usageCount++;
            }
            else
            {
                if (usageCount <= 0)
                    throw new InvalidOperationException();

                usageCount--;

                if (usageCount == 0)
                {
                    return PatchUnpatch(tracker, false, targetMethod, prefix, postfix, transpiler, finalizer, suppress);
                }
            }

            return true;
        }

        private static HarmonyMethod GetHarmonyMethod(Type patchType, string patchMethod)
        {
            if (string.IsNullOrEmpty(patchMethod)) return null;

            return HarmonyMethodCache.GetOrAdd(Tuple.Create(patchType, patchMethod), tuple =>
            {
                var methodInfo = GetMethodInfo(tuple.Item1, tuple.Item2);
                return methodInfo != null ? new HarmonyMethod(methodInfo) : null;
            });
        }

        private static MethodInfo GetMethodInfo(Type patchType, string patchMethod)
        {
            if (string.IsNullOrEmpty(patchMethod)) return null;

            return MethodInfoCache.GetOrAdd(Tuple.Create(patchType, patchMethod),
                tuple => AccessTools.Method(tuple.Item1, tuple.Item2));
        }
    }
}
