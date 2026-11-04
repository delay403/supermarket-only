using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
	/// <summary>将 MainHome 场景加入/移出 Build Settings，并固定在 index 2。</summary>
	public static class MainHomeBuildSettingsSync
	{
		public const string DefaultMainHomeScenePath = Game.Core.Content.GameContentPaths.MainHomeScenePath;
		public const int MainHomeBuildIndex = 2;

		public static string NormalizeScenePath(string assetPath)
		{
			if (string.IsNullOrWhiteSpace(assetPath))
				return null;
			return assetPath.Replace('\\', '/').Trim();
		}

		public static bool IsSceneInBuild(string sceneAssetPath)
		{
			sceneAssetPath = NormalizeScenePath(sceneAssetPath);
			if (string.IsNullOrEmpty(sceneAssetPath))
				return false;
			return EditorBuildSettings.scenes.Any(s =>
				string.Equals(NormalizeScenePath(s.path), sceneAssetPath, StringComparison.OrdinalIgnoreCase));
		}

		public static int GetBuildIndex(string sceneAssetPath)
		{
			sceneAssetPath = NormalizeScenePath(sceneAssetPath);
			if (string.IsNullOrEmpty(sceneAssetPath))
				return -1;
			var scenes = EditorBuildSettings.scenes;
			for (int i = 0; i < scenes.Length; i++)
			{
				if (string.Equals(NormalizeScenePath(scenes[i].path), sceneAssetPath, StringComparison.OrdinalIgnoreCase))
					return i;
			}
			return -1;
		}

		/// <summary>启用时插入 index 2；禁用时从列表移除（若存在）。</summary>
		public static void Apply(string sceneAssetPath, bool useMainHome)
		{
			sceneAssetPath = NormalizeScenePath(sceneAssetPath);
			if (string.IsNullOrEmpty(sceneAssetPath))
			{
				Debug.LogWarning("[MainHomeBuildSettingsSync] 场景路径无效，无法更新 Build Settings。");
				return;
			}

			if (!System.IO.File.Exists(sceneAssetPath))
			{
				Debug.LogWarning($"[MainHomeBuildSettingsSync] 场景文件不存在: {sceneAssetPath}");
				return;
			}

			var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
			list.RemoveAll(s => string.Equals(
				NormalizeScenePath(s.path), sceneAssetPath, StringComparison.OrdinalIgnoreCase));

			if (useMainHome)
			{
				var insertAt = Mathf.Clamp(MainHomeBuildIndex, 0, list.Count);
				list.Insert(insertAt, new EditorBuildSettingsScene(sceneAssetPath, true));
			}

			EditorBuildSettings.scenes = list.ToArray();
		}
	}
}
