using System;
using System.Collections.Generic;
using Game.SupermarketOrganize.Config;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>摄像机前手持区：物品按宽度沿本地 Y 轴堆叠，拾取后本地坐标不再变化。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketHandleZone : MonoBehaviour
    {
        [SerializeField] private int maxCapacity = 16;
        [SerializeField] private float slotLocalX = 0.25f;
        [SerializeField] private float slotGap = 0.05f;

        readonly List<SupermarketItem> _items = new List<SupermarketItem>();
        int _currentIndex;

        public int Count => _items.Count;
        public int MaxCapacity => Mathf.Max(1, maxCapacity);
        public bool IsFull => _items.Count >= MaxCapacity;
        public int CurrentIndex => _currentIndex;

        public SupermarketItem CurrentItem =>
            _items.Count == 0 ? null : _items[Mathf.Clamp(_currentIndex, 0, _items.Count - 1)];

        public event Action CurrentItemChanged;

        public void ApplyConfig(SupermarketOrganizeConfig config)
        {
            if (config == null)
                return;

            maxCapacity = Mathf.Clamp(config.defaultHandleCapacity, 1, config.maxHandleCapacity);
            slotLocalX = config.handleSlotLocalX;
            slotGap = config.handleSlotGap;
        }

        public void SetMaxCapacity(int capacity)
        {
            maxCapacity = Mathf.Max(1, capacity);
            while (_items.Count > maxCapacity)
                _items.RemoveAt(_items.Count - 1);

            _currentIndex = _items.Count == 0 ? 0 : Mathf.Clamp(_currentIndex, 0, _items.Count - 1);
        }

        public bool TryAdd(SupermarketItem item, out int slotIndex)
        {
            slotIndex = -1;
            if (item == null || IsFull)
                return false;

            slotIndex = _items.Count;
            _items.Add(item);
            _currentIndex = slotIndex;
            CurrentItemChanged?.Invoke();
            return true;
        }

        public bool TryRemove(SupermarketItem item)
        {
            var index = _items.IndexOf(item);
            if (index < 0)
                return false;

            _items.RemoveAt(index);
            if (_items.Count == 0)
                _currentIndex = 0;
            else if (_currentIndex >= _items.Count)
                _currentIndex = _items.Count - 1;

            RelayoutHeldItems();
            CurrentItemChanged?.Invoke();
            return true;
        }

        public void ClearAll()
        {
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];
                if (item != null)
                    Destroy(item.gameObject);
            }

            _items.Clear();
            _currentIndex = 0;
            CurrentItemChanged?.Invoke();
        }

        public Vector3 GetSlotLocalPosition(int slotIndex)
        {
            if (_items.Count == 0 || slotIndex < 0)
                return new Vector3(slotLocalX, 0f, 0f);

            slotIndex = Mathf.Clamp(slotIndex, 0, _items.Count - 1);
            var y = 0f;
            for (var i = 1; i <= slotIndex; i++)
            {
                y += GetItemHalfWidth(_items[i - 1])
                     + slotGap
                     + GetItemHalfWidth(_items[i]);
            }

            return new Vector3(slotLocalX, y, 0f);
        }

        public Quaternion GetSlotLocalRotation(int slotIndex)
        {
            if (Count < 2 || slotIndex < 0 || slotIndex >= _items.Count)
                return Quaternion.identity;

            var item = _items[slotIndex];
            return item != null
                ? Quaternion.Euler(item.MultiHoldLocalRotationEuler)
                : Quaternion.identity;
        }

        public Vector3 GetSlotWorldPosition(int slotIndex) =>
            transform.TransformPoint(GetSlotLocalPosition(slotIndex));

        public Quaternion GetSlotWorldRotation() => transform.rotation;

        public void AttachToSlot(SupermarketItem item, int slotIndex)
        {
            if (item == null)
                return;

            item.PrepareForHandle();
            ApplySlotLocalTransform(item.transform, slotIndex);
        }

        public void ApplySlotLocalTransform(Transform itemTransform, int slotIndex)
        {
            if (itemTransform == null)
                return;

            if (itemTransform.parent != transform)
                itemTransform.SetParent(transform, false);

            itemTransform.localPosition = GetSlotLocalPosition(slotIndex);
            itemTransform.localRotation = GetSlotLocalRotation(slotIndex);
        }

        /// <summary>放置后压缩堆叠；已在手中的物品仅更新本地坐标，不再做世界移动。</summary>
        public void RelayoutHeldItems()
        {
            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == null || item.State != SupermarketItemState.InHandle)
                    continue;

                ApplySlotLocalTransform(item.transform, i);
                item.gameObject.SetActive(true);
            }
        }

        public IReadOnlyList<SupermarketItem> Items => _items;

        public bool TryCycleCurrentItem()
        {
            if (_items.Count <= 1)
                return false;

            _currentIndex = (_currentIndex + 1) % _items.Count;
            RelayoutHeldItems();
            CurrentItemChanged?.Invoke();
            return true;
        }

        static float GetItemHalfWidth(SupermarketItem item) =>
            Mathf.Max(0.01f, item != null ? item.HandleWidth : 0.2f) * 0.5f;

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.55f, 1f, 0.85f);
            var previewCount = Application.isPlaying ? _items.Count : MaxCapacity;
            for (var i = 0; i < previewCount; i++)
            {
                var pos = Application.isPlaying
                    ? GetSlotLocalPosition(i)
                    : new Vector3(slotLocalX, i * (0.2f + slotGap), 0f);
                Gizmos.DrawWireSphere(transform.TransformPoint(pos), 0.04f);
            }
        }
    }
}
