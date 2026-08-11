#if UNITY_EDITOR
using TJGenerators.Generators;

namespace TJGenerators.Pipeline
{
    /// <summary>
    /// 生成流水线媒体资产类型（纹理/音频/视频）。
    /// </summary>
    public enum PipelineMediaType
    {
        Texture,
        Audio,
        Video,
    }

    public interface IGenerationPipelineHost
    {
        TJGeneratorsAssetReference GetTargetAsset();
        void RefreshHistory();
        void ShowPreviewModel(string assetPath);
        void RefreshUserInfo();
        void Repaint();
        void StartGeneration(ModelGeneratorBase generator);
        void ShowDialog(string title, string message);

        /// <summary>
        /// 获取指定类型媒体资产的保存路径。
        /// 返回 null 表示该 Host 不处理此类媒体。
        /// </summary>
        string GetAssetSavePath(PipelineMediaType _type, ModelGeneratorBase generator);

        /// <summary>
        /// 指定类型媒体资产下载保存后的回调（Import Settings、历史刷新、打标签等）。
        /// </summary>
        void OnAssetSaved(PipelineMediaType _type, string savePath, ModelGeneratorBase generator);
    }
}
#endif
