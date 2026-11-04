using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Visual
{
    /// <summary>移除交互高亮、描边叠加材质，恢复手持/装备模型的基础外观。</summary>
    public static class InteractableEffectVisualUtility
    {
        public static void PrepareHandVisualFromSource(GameObject sourceRoot, GameObject handVisualRoot)
        {
            if (handVisualRoot == null)
                return;

            // 克隆体上的 Outline.OnEnable 会立刻追加描边材质，必须先拆掉再清材质。
            RemoveOutlineComponents(handVisualRoot);

            if (sourceRoot != null)
                CopySharedMaterials(sourceRoot, handVisualRoot);

            // 无论来源是否干净，手持模型最终都只保留基础材质。
            StripEffectMaterials(handVisualRoot);
        }

        public static void StripEffectMaterials(GameObject root)
        {
            if (root == null)
                return;

            RemoveOutlineComponents(root);

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                // 用 materials（实例）过滤，避免 sharedMaterials 与实例槽位不一致。
                var filtered = FilterBaseMaterials(renderer.materials);
                if (filtered.Length > 0)
                    renderer.sharedMaterials = filtered;
                else
                    renderer.sharedMaterials = Array.Empty<Material>();
            }
        }

        static void CopySharedMaterials(GameObject sourceRoot, GameObject targetRoot)
        {
            var sourceRenderers = sourceRoot.GetComponentsInChildren<Renderer>(true);
            var targetRenderers = targetRoot.GetComponentsInChildren<Renderer>(true);
            if (sourceRenderers.Length != targetRenderers.Length)
                return;

            for (var i = 0; i < sourceRenderers.Length; i++)
            {
                var source = sourceRenderers[i];
                var target = targetRenderers[i];
                if (source == null || target == null)
                    continue;

                target.sharedMaterials = FilterBaseMaterials(source.sharedMaterials);
            }
        }

        static void RemoveOutlineComponents(GameObject root)
        {
            var outlines = root.GetComponentsInChildren<Outline>(true);
            for (var i = outlines.Length - 1; i >= 0; i--)
            {
                var outline = outlines[i];
                if (outline == null)
                    continue;

                // 先禁用以触发 OnDisable 移除描边材质，再销毁组件。
                outline.enabled = false;
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(outline);
                else
                    UnityEngine.Object.DestroyImmediate(outline);
            }
        }

        static Material[] FilterBaseMaterials(IReadOnlyList<Material> materials)
        {
            if (materials == null || materials.Count == 0)
                return Array.Empty<Material>();

            var filtered = new List<Material>(materials.Count);
            for (var i = 0; i < materials.Count; i++)
            {
                var mat = materials[i];
                if (!IsEffectMaterial(mat))
                    filtered.Add(mat);
            }

            return filtered.ToArray();
        }

        public static bool IsEffectMaterial(Material material)
        {
            if (material == null)
                return false;

            // Unity 实例材质名常带 " (Instance)"；QuickOutline 用 OutlineFill/OutlineMask（无空格）。
            var matName = material.name.Replace(" (Instance)", string.Empty);
            if (ContainsIgnoreCase(matName, "Outline Fill")
                || ContainsIgnoreCase(matName, "OutlineFill")
                || ContainsIgnoreCase(matName, "Outline Mask")
                || ContainsIgnoreCase(matName, "OutlineMask")
                || ContainsIgnoreCase(matName, "High Light")
                || ContainsIgnoreCase(matName, "Highlight Overlay")
                || ContainsIgnoreCase(matName, "HighLight"))
                return true;

            var shader = material.shader;
            if (shader == null)
                return false;

            var shaderName = shader.name;
            return ContainsIgnoreCase(shaderName, "Outline")
                   || ContainsIgnoreCase(shaderName, "High Light")
                   || ContainsIgnoreCase(shaderName, "Highlight");
        }

        static bool ContainsIgnoreCase(string value, string token) =>
            !string.IsNullOrEmpty(value)
            && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
