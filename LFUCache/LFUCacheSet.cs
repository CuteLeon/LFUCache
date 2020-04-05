using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace LFUCache
{
    public class LFUCacheSet<TCacheKey, TCacheValue>
    {
        internal sealed class CacheEntity
        {
            public TCacheKey Key { get; }
            public TCacheValue Value { get; set; }
            public int Frequency { get; set; } = 0;
            public CacheEntity(TCacheKey key, TCacheValue value)
            {
                this.Key = key;
                this.Value = value;
            }
        }

        Dictionary<TCacheKey, CacheEntity> entityDictionary = new Dictionary<TCacheKey, CacheEntity>();
        Dictionary<int, HashSet<CacheEntity>> frequencyDictionary = new Dictionary<int, HashSet<CacheEntity>>();
        private readonly int capacity;
        public event EventHandler<string> Log;
        private SpinLock spinLock = new SpinLock();
        private SortedSet<int> frequencySortedSet = new SortedSet<int>();

        /// <summary>
        /// LFU 缓存容器
        /// </summary>
        /// <param name="capacity">缓存容量</param>
        public LFUCacheSet(int capacity = 100)
        {
            if (capacity < 1)
            {
                throw new ArgumentException(nameof(capacity));
            }

            Log?.Invoke(this, $"创建 LFU 缓存容器[{this.GetHashCode():X}]：Capacity={capacity}");
            this.capacity = capacity;
        }

        #region 实体操作

        /// <summary>
        /// 添加元素
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public TCacheValue Add(TCacheKey key, TCacheValue value)
        {
            var lockSeed = false;
            if (!this.spinLock.IsHeldByCurrentThread)
            {
                this.spinLock.Enter(ref lockSeed);
            }

            CacheEntity currentEntity = null;
            TCacheValue removeValue = default;
            if (entityDictionary.ContainsKey(key))
            {
                Log?.Invoke(this, $"覆盖已有的键：{key}={value}");
                currentEntity = entityDictionary[key];
                currentEntity.Value = value;

                RemoveFromSet(currentEntity);
                currentEntity.Frequency++;
            }
            else
            {
                Log?.Invoke(this, $"新增缓存：{key}={value}");
                currentEntity = new CacheEntity(key, value);
                this.entityDictionary.Add(key, currentEntity);
                if (entityDictionary.Count > capacity)
                {
                    var removeEntity = GetLeastFrequentEntity();
                    if (removeEntity != null)
                    {
                        removeValue = removeEntity.Value;
                        RemoveFromSet(removeEntity);
                    }
                }
            }

            AddToSet(currentEntity);

            if (lockSeed)
            {
                this.spinLock.Exit();
            }

            return removeValue;
        }

        /// <summary>
        /// 移除缓存
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public TCacheValue Remove(TCacheKey key)
        {
            if (!this.entityDictionary.ContainsKey(key))
            {
                Log?.Invoke(this, $"无法删除不存在的Key：{key}");
                return default;
            }

            var lockSeed = false;
            if (!this.spinLock.IsHeldByCurrentThread)
            {
                this.spinLock.Enter(ref lockSeed);
            }

            var currentEntity = this.entityDictionary[key];
            this.entityDictionary.Remove(key);
            Log?.Invoke(this, $"删除缓存：{currentEntity.Key}={currentEntity.Value}");

            RemoveFromSet(currentEntity);
            if (lockSeed)
            {
                this.spinLock.Exit();
            }

            return currentEntity.Value;
        }

        /// <summary>
        /// 获取缓存
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public TCacheValue Get(TCacheKey key)
        {
            if (!this.entityDictionary.ContainsKey(key))
            {
                Log?.Invoke(this, $"使用Key不存在的缓存：{key}");
                return default;
            }

            var lockSeed = false;
            if (!this.spinLock.IsHeldByCurrentThread)
            {
                this.spinLock.Enter(ref lockSeed);
            }

            var currentEntity = this.entityDictionary[key];
            Log?.Invoke(this, $"使用缓存：{currentEntity.Key}={currentEntity.Value}");

            RemoveFromSet(currentEntity);
            currentEntity.Frequency++;
            AddToSet(currentEntity);

            if (lockSeed)
            {
                this.spinLock.Exit();
            }

            return currentEntity.Value;
        }
        #endregion

        #region 频率操作

        private CacheEntity GetLeastFrequentEntity()
        {
            if (frequencySortedSet.Count == 0)
                return default;
            var leastFrequency = frequencySortedSet.Min;
            return frequencyDictionary.ContainsKey(leastFrequency) ?
                frequencyDictionary[leastFrequency].FirstOrDefault() :
                null;
        }

        private void RemoveFromSet(CacheEntity currentEntity)
        {
            if (!frequencyDictionary.ContainsKey(currentEntity.Frequency))
                return;
            var frequencySet = frequencyDictionary[currentEntity.Frequency];
            frequencySet.Remove(currentEntity);
            if (frequencySortedSet.Min == currentEntity.Frequency && frequencySet.Count == 0)
            {
                frequencyDictionary.Remove(currentEntity.Frequency);
                frequencySortedSet.Remove(frequencySortedSet.Min);
            }
        }

        private void AddToSet(CacheEntity currentEntity)
        {
            if (!frequencyDictionary.ContainsKey(currentEntity.Frequency))
            {
                frequencyDictionary.Add(currentEntity.Frequency, new HashSet<CacheEntity>());
                frequencySortedSet.Add(currentEntity.Frequency);
            }

            if (!frequencyDictionary[currentEntity.Frequency].Contains(currentEntity))
            {
                frequencyDictionary[currentEntity.Frequency].Add(currentEntity);
            }
        }
        #endregion
    }
}
