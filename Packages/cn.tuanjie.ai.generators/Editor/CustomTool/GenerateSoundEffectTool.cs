using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Codely.Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
using TJGenerators;
using TJGenerators.Generators;
using TJGenerators.Config;
using TJGenerators.Pipeline;
using TJGenerators.Utils;
using Unity.EditorCoroutines.Editor;
#endif

namespace UnityTcp.Editor.Tools
{
    /// <summary>
    /// CustomTool for generating sound effects (SFX) using TJGenerators Music pipeline (sound-effect generator).
    /// Supports text-to-audio generation for one-shot sound effects such as gunshots, footsteps, explosions, UI clicks, etc.
    /// Output is an audio asset saved to Assets/TJGenerators/History/.
    /// Domain-reload recovery is handled by AudioDomainReloadRecovery in GenerateAudioClipTool.cs
    /// (shares AudioClipTaskTracker and AudioPipelineHost).
    /// </summary>
    public static class GenerateSoundEffectTool
    {
        [ExecuteCustomTool.CustomTool("generate_sound_effect",
            "Generate a sound effect (SFX) from a text prompt using AI. " +
            "This tool is for one-shot sound effects ONLY — NOT for background music or looping ambient audio. " +
            "Use for: gunshots, footsteps, explosions, UI clicks, item pickups, environmental sounds, etc. " +
            "Output is an AudioClip asset (format depends on output_format) saved to Assets/TJGenerators/History/. " +
            "Parameters: prompt (text description of the sound effect, required), " +
            "duration_seconds (optional float, 1-22 seconds, default 5), " +
            "prompt_influence (optional float, 0-1, default 0.5 — how strongly the prompt shapes the result), " +
            "output_format (optional fal enum, default 'mp3_44100_128'; " +
            "valid: 'mp3_22050_32'|'mp3_44100_32'|'mp3_44100_64'|'mp3_44100_96'|'mp3_44100_128'|'mp3_44100_192'|" +
            "'pcm_8000'|'pcm_16000'|'pcm_22050'|'pcm_24000'|'pcm_44100'|'pcm_48000'; " +
            "do NOT pass file extensions like 'wav' or 'mp3'), " +
            "loop (optional bool, default false — whether to generate a loopable sound effect), " +
            "output_path (optional asset save path). " +
            "IMPORTANT: Generation takes 10-60 seconds. After calling this tool, wait at least 5 seconds " +
            "before the first query_sound_effect_status call, then poll every 5-10 seconds. " +
            "A placeholder_path is returned immediately — you can assign it to an AudioSource right away.")]
        public static object GenerateSoundEffect(JObject parameters)
        {
#if UNITY_EDITOR
            try
            {
                TJLog.Log($"[GenerateSoundEffectTool] Generating sound effect with parameters: {parameters}");

                string prompt = parameters["prompt"]?.ToString();
                string outputPath = parameters["output_path"]?.ToString();
                string sessionId = parameters["session_id"]?.ToString() ?? "";
                bool playOnAwake = parameters["play_on_awake"] != null ? parameters["play_on_awake"].ToObject<bool>() : false;

                if (string.IsNullOrEmpty(prompt))
                {
                    return new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", "'prompt' parameter is required" }
                    };
                }

                int maxLen = TJGeneratorsPromptLimits.GetMaxLength("sound-effect");
                if (prompt.Length > maxLen)
                {
                    return new Dictionary<string, object>
                    {
                        { "success", false },
                        { "error_code", "INVALID_PARAMS" },
                        { "message", $"Prompt length ({prompt.Length}) exceeds the {maxLen} character limit. Please shorten your sound effect description." }
                    };
                }

                // Load sound-effect generator config
                var config = ConfigManager.GetGeneratorConfig(ConfigType.Music, "sound-effect");
                if (config == null)
                {
                    return new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", "Cannot find generator config for 'sound-effect'. Ensure the TJGenerators package is installed and the Editor has finished compiling." }
                    };
                }

                // Create generator and set inputs
                var generator = new DynamicGenerator(config);
                generator.SetTextPrompt(prompt);

                // Apply optional parameters
                ApplySfxParameters(generator, parameters);

                // 阶段1：同步提交任务到后端，立即获取 backendTaskId 或失败原因
                var submitResult = TJGeneratorsGenerationService.SubmitTaskSync(generator, sessionId);
                if (!submitResult.Success)
                {
                    TJLog.LogError($"[GenerateSoundEffectTool] 任务提交失败 [{submitResult.ErrorCode}]: {submitResult.Message}");
                    return new Dictionary<string, object>
                    {
                        { "success",    false },
                        { "error_code", submitResult.ErrorCode },
                        { "message",    submitResult.Message }
                    };
                }

                TJLog.Log($"[GenerateSoundEffectTool] 任务提交成功，backend_task_id={submitResult.BackendTaskId}");

                // 提交成功后才创建 placeholder（避免在鉴权失败时留下无用文件）
                var (placeholderPath, audioDownloadPath) = BuildSfxPaths(outputPath, generator);

                // Create tracked task (reuse shared AudioClipTaskTracker)
                string capturedBackendTaskId = submitResult.BackendTaskId;
                string taskId = AudioClipTaskTracker.CreateTask("sound-effect", prompt, placeholderPath, capturedBackendTaskId);

                // Create pipeline host with audio-specific callbacks
                var host = new AudioPipelineHost(placeholderPath, audioDownloadPath, sessionId, isBgm: false, playOnAwake: playOnAwake,
                    (savedPath, previewUrl) =>
                    {
                        AudioClipTaskTracker.MarkCompleted(taskId, savedPath, previewUrl);
                        var t = AudioClipTaskTracker.GetTask(taskId);
                        GenerationNotifier.NotifyCompleted("generate_sound_effect", taskId, capturedBackendTaskId,
                            new JObject
                            {
                                ["session_id"]       = sessionId,
                                ["generator_id"]     = "sound-effect",
                                ["prompt"]           = prompt ?? "",
                                ["audio_path"]       = savedPath ?? "",
                                ["preview_url"]      = previewUrl ?? "",
                                ["progress"]         = 100,
                                ["start_time"]       = t?.StartTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                                ["end_time"]         = t?.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                                ["duration_seconds"] = (t != null && t.EndTime.HasValue) ? (int)(t.EndTime.Value - t.StartTime).TotalSeconds : 0
                            });
                    },
                    errorMsg =>
                    {
                        AudioClipTaskTracker.MarkFailed(taskId, errorMsg);
                        GenerationNotifier.NotifyFailed("generate_sound_effect", taskId, capturedBackendTaskId, errorMsg,
                            new JObject { ["session_id"] = sessionId, ["generator_id"] = "sound-effect", ["prompt"] = prompt ?? "" });
                    });

                // 阶段2：异步轮询（跳过提交）。Domain reload 恢复见 GenerateAudioClipTool.AudioDomainReloadRecovery。
                var pipeline = new GenerationPipeline(host, ConfigType.Music, GenerationRequestOrigin.Agent, sessionId, "generate_sound_effect");
                string historyAssetGuid = CustomToolHistoryBindings.HistoryGuidFromPlaceholderAssetPath(placeholderPath);
                EditorCoroutineUtility.StartCoroutineOwnerless(
                    pipeline.StartFromSubmittedTask(generator, historyAssetGuid, submitResult.BackendTaskId));

                TJLog.Log($"[GenerateSoundEffectTool] 轮询已启动，task_id={taskId}, backend_task_id={submitResult.BackendTaskId}, placeholder: {placeholderPath}, download: {audioDownloadPath}");

                return new Dictionary<string, object>
                {
                    { "success",            true },
                    { "submission_success", true },
                    { "message",
                        "Sound effect generation started. " +
                        "STEP 1 (do now): Note the placeholder_path — a silent placeholder is available immediately. " +
                        "STEP 2 (critical): END THIS RESPONSE TURN immediately. " +
                        "STEP 3 (automatic): A <bg_task_done> notification will appear in your next turn (~30s) " +
                        "containing ALL generation results (audio_path, preview_url, timing, etc.). " +
                        "*** POLLING IS STRICTLY FORBIDDEN — do NOT call query_sound_effect_status repeatedly. " +
                        "Only call query_sound_effect_status ONCE as a last-resort fallback if no notification arrives. ***" },
                    { "task_id",            taskId },
                    { "backend_task_id",    submitResult.BackendTaskId },
                    { "status",             "submitted" },
                    { "generator_id",       "sound-effect" },
                    { "prompt",             prompt },
                    { "placeholder_path",   placeholderPath },
                    { "estimated_wait_seconds", 30 },
                    { "notification_mode",  "bg_task_done" },
                    { "preview_url",        PreviewUrlHelper.BuildFixedPreviewUrl(submitResult.BackendTaskId) }
                };
            }
            catch (Exception e)
            {
                TJLog.LogError($"[GenerateSoundEffectTool] Error: {e}");
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", $"Error generating sound effect: {e.Message}" }
                };
            }
#else
            return new Dictionary<string, object>
            {
                { "success", false },
                { "message", "This tool only works in Unity Editor." }
            };
#endif
        }

        [ExecuteCustomTool.CustomTool("query_sound_effect_status",
            "Query the status of a sound effect generation task. Use ONLY as a one-time fallback if no <bg_task_done> notification arrives. " +
            "When completed, returns 'audio_path' with the AudioClip asset path in the project. " +
            "Status values: 'generating', 'completed', 'failed'. " +
            "WARNING: Do NOT call this tool repeatedly. Polling is forbidden.")]
        public static object QuerySoundEffectStatus(JObject parameters)
        {
#if UNITY_EDITOR
            try
            {
                string taskId = parameters["task_id"]?.ToString();

                if (string.IsNullOrEmpty(taskId))
                {
                    return new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", "'task_id' parameter is required" }
                    };
                }

                var task = AudioClipTaskTracker.GetTask(taskId);

                if (task == null)
                {
                    return new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", $"Task '{taskId}' not found. It may have been completed and cleaned up." }
                    };
                }

                var result = new Dictionary<string, object>
                {
                    { "success", true },
                    { "task_id", task.TaskId },
                    { "generator_id", task.GeneratorId },
                    { "status", task.Status },
                    { "progress", task.Progress },
                    { "prompt", task.Prompt },
                    { "start_time", task.StartTime.ToString("yyyy-MM-dd HH:mm:ss") }
                };

                if (!string.IsNullOrEmpty(task.AudioPath))
                    result["audio_path"] = task.AudioPath;

                result["preview_url"] = PreviewUrlHelper.GetPreviewUrl(task.PreviewUrl, task.BackendTaskId);

                if (!string.IsNullOrEmpty(task.ErrorMessage))
                    result["error"] = task.ErrorMessage;

                if (task.EndTime.HasValue)
                {
                    result["end_time"] = task.EndTime.Value.ToString("yyyy-MM-dd HH:mm:ss");
                    result["duration_seconds"] = (int)(task.EndTime.Value - task.StartTime).TotalSeconds;
                }

                if (task.Status == "generating")
                {
                    if (!string.IsNullOrEmpty(task.PlaceholderPath))
                        result["placeholder_path"] = task.PlaceholderPath;
                }

                return result;
            }
            catch (Exception e)
            {
                TJLog.LogError($"[GenerateSoundEffectTool] Query error: {e}");
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", $"Error querying task status: {e.Message}" }
                };
            }
#else
            return new Dictionary<string, object>
            {
                { "success", false },
                { "message", "This tool only works in Unity Editor." }
            };
#endif
        }

        [ExecuteCustomTool.CustomTool("list_sound_effect_tasks", "List all active and recent sound effect generation tasks")]
        public static object ListSoundEffectTasks(JObject parameters)
        {
#if UNITY_EDITOR
            try
            {
                // Filter to only show sound-effect tasks from the shared tracker
                var allTasks = AudioClipTaskTracker.GetAllTasks();
                var taskList = new List<Dictionary<string, object>>();

                foreach (var task in allTasks)
                {
                    if (task.GeneratorId != "sound-effect")
                        continue;

                    var taskData = new Dictionary<string, object>
                    {
                        { "task_id", task.TaskId },
                        { "generator_id", task.GeneratorId },
                        { "status", task.Status },
                        { "progress", task.Progress },
                        { "prompt", task.Prompt },
                        { "start_time", task.StartTime.ToString("yyyy-MM-dd HH:mm:ss") }
                    };

                    if (!string.IsNullOrEmpty(task.AudioPath))
                        taskData["audio_path"] = task.AudioPath;

                    taskData["preview_url"] = PreviewUrlHelper.GetPreviewUrl(task.PreviewUrl, task.BackendTaskId);

                    if (!string.IsNullOrEmpty(task.ErrorMessage))
                        taskData["error"] = task.ErrorMessage;

                    if (task.EndTime.HasValue)
                        taskData["end_time"] = task.EndTime.Value.ToString("yyyy-MM-dd HH:mm:ss");

                    taskList.Add(taskData);
                }

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "count", taskList.Count },
                    { "tasks", taskList }
                };
            }
            catch (Exception e)
            {
                TJLog.LogError($"[GenerateSoundEffectTool] List error: {e}");
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", $"Error listing tasks: {e.Message}" }
                };
            }
#else
            return new Dictionary<string, object>
            {
                { "success", false },
                { "message", "This tool only works in Unity Editor." }
            };
#endif
        }

#if UNITY_EDITOR
        private static (string placeholderPath, string downloadPath) BuildSfxPaths(string outputPath, DynamicGenerator generator)
        {
            // Resolve the extension from the generator's effective AudioFormat (which reflects the
            // user-requested output_format, falling back to the config default). The fal.ai enum
            // (e.g. "mp3_44100_128", "pcm_44100") is mapped to a real file extension so the placeholder
            // extension aligns with what the pipeline detects from the backend's magic bytes — keeping
            // the download overwrite in-place instead of leaving a stale placeholder behind.
            string ext = "." + FalEnumToAudioExtension(generator?.AudioFormat ?? "mp3");
            string audioPath;
            if (!string.IsNullOrEmpty(outputPath))
            {
                string dir = Path.GetDirectoryName(outputPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir))
                    EnsureAssetDatabaseFolder(dir);
                string stem = Path.GetFileNameWithoutExtension(outputPath);
                if (string.IsNullOrEmpty(stem))
                    stem = "SFX";
                audioPath = TJGenerators.Utils.TJGeneratorsAudioAssetPathUtility.GenerateUniqueAudioPath(
                    $"{dir}/{stem}{ext}");
            }
            else
            {
                if (!AssetDatabase.IsValidFolder("Assets/TJGenerators"))
                    AssetDatabase.CreateFolder("Assets", "TJGenerators");
                if (!AssetDatabase.IsValidFolder("Assets/TJGenerators/History"))
                    AssetDatabase.CreateFolder("Assets/TJGenerators", "History");
                string uniqueName = "SFX_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                audioPath = TJGenerators.Utils.TJGeneratorsAudioAssetPathUtility.GenerateUniqueAudioPath(
                    "Assets/TJGenerators/History/" + uniqueName + ext);
            }

            // Create a blank placeholder (extension matches the resolved format) so the AI Agent can
            // assign it immediately. CreateBlankAudioClip forces .wav; for non-wav formats use the
            // matching blank creator so the placeholder extension equals the download extension.
            string placeholderPath;
            if (string.Equals(ext, ".wav", StringComparison.OrdinalIgnoreCase))
                placeholderPath = TJGeneratorsAudioUtils.CreateBlankAudioClip(audioPath);
            else if (string.Equals(ext, ".mp3", StringComparison.OrdinalIgnoreCase))
                placeholderPath = TJGeneratorsAudioUtils.CreateBlankMp3Clip(audioPath);
            else
                placeholderPath = TJGeneratorsAudioUtils.CreateBlankAudioClip(audioPath);

            // downloadPath shares the same base path; the pipeline may change the extension when it
            // detects the real format from magic bytes, but with matching formats it overwrites in-place.
            return (placeholderPath, audioPath);
        }

        private static void EnsureAssetDatabaseFolder(string folderPath)
        {
            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void ApplySfxParameters(DynamicGenerator generator, JObject parameters)
        {
            if (parameters["duration_seconds"] != null)
                generator.SetParameter("durationSeconds", parameters["duration_seconds"].ToObject<float>());

            if (parameters["prompt_influence"] != null)
                generator.SetParameter("promptInfluence", parameters["prompt_influence"].ToObject<float>());

            // fal.ai expects codec_rate_bitrate enums (e.g. mp3_44100_128), not file extensions
            // like "wav"/"mp3". Agents often confuse the two; normalize before submit.
            generator.SetParameter(
                "outputFormat",
                NormalizeSfxOutputFormat(parameters["output_format"]?.ToString()));

            if (parameters["loop"] != null)
                generator.SetParameter("loop", parameters["loop"].ToObject<bool>());
        }

        /// <summary>Test hook for offline parameter-mapping checks.</summary>
        internal static void ApplySfxParametersInternal(DynamicGenerator generator, JObject parameters)
            => ApplySfxParameters(generator, parameters);

        /// <summary>
        /// Maps fal.ai sound-effect <c>output_format</c> enums (or plain aliases) to a file
        /// extension without the leading dot (e.g. <c>mp3_44100_128</c> → <c>mp3</c>).
        /// </summary>
        internal static string FalEnumToAudioExtension(string format)
        {
            return TJGenerators.Utils.TJGeneratorsAudioAssetPathUtility
                .NormalizeImportedAudioFileExtension(format);
        }

        /// <summary>
        /// Maps aliases / empty values to fal.ai sound-effects <c>output_format</c> enums.
        /// Default matches fal docs (mp3_44100_128); the placeholder extension is derived from the
        /// resolved AudioFormat so it stays aligned with the generated audio.
        /// </summary>
        internal static string NormalizeSfxOutputFormat(string format)
        {
            const string defaultFormat = "mp3_44100_128";
            if (string.IsNullOrWhiteSpace(format))
                return defaultFormat;

            string fmt = format.Trim();
            // File-extension aliases are not valid fal enums; map to the default enum. The placeholder
            // extension is resolved from generator.AudioFormat, so it tracks the actual output format.
            if (string.Equals(fmt, "mp3", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fmt, "wav", StringComparison.OrdinalIgnoreCase))
                return defaultFormat;
            if (string.Equals(fmt, "pcm", StringComparison.OrdinalIgnoreCase))
                return "pcm_44100";

            return fmt;
        }
#endif
    }
}
