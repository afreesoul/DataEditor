using System;
using System.Collections;
using System.Collections.Generic;

namespace GameDataEditor.Models.Utils
{
    /// <summary>
    /// 简化的固定长度数组类
    /// 专为Monster数据设计，避免JSON序列化问题
    /// </summary>
    /// <typeparam name="T">数组元素类型</typeparam>
    public class SimpleFixedArray<T> : IEnumerable<T>
    {
        private readonly T[] _items;

        /// <summary>
        /// 构造函数，指定数组长度
        /// </summary>
        /// <param name="length">数组长度</param>
        public SimpleFixedArray(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "数组长度不能为负数");
                
            _items = new T[length];
            
            // 使用默认值初始化数组
            for (int i = 0; i < length; i++)
            {
                _items[i] = default(T)!;
            }
        }

        /// <summary>
        /// 获取数组长度
        /// </summary>
        public int Length => _items.Length;

        /// <summary>
        /// 获取或设置指定索引处的元素
        /// </summary>
        /// <param name="index">索引</param>
        /// <returns>元素值</returns>
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _items.Length)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _items[index];
            }
            set
            {
                if (index < 0 || index >= _items.Length)
                    throw new ArgumentOutOfRangeException(nameof(index));
                _items[index] = value;
            }
        }

        /// <summary>
        /// 返回循环访问数组的枚举器
        /// </summary>
        /// <returns>数组枚举器</returns>
        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)_items).GetEnumerator();
        }

        /// <summary>
        /// 返回循环访问数组的枚举器
        /// </summary>
        /// <returns>数组枚举器</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        /// <summary>
        /// 将数组转换为List（用于JSON序列化）
        /// </summary>
        /// <returns>List表示</returns>
        public List<T> ToList()
        {
            return new List<T>(_items);
        }

        /// <summary>
        /// 从List创建SimpleFixedArray（用于JSON反序列化）
        /// </summary>
        /// <param name="list">源列表</param>
        /// <param name="targetLength">目标长度</param>
        /// <returns>SimpleFixedArray实例</returns>
        public static SimpleFixedArray<T> FromList(List<T> list, int targetLength)
        {
            var array = new SimpleFixedArray<T>(targetLength);
            for (int i = 0; i < Math.Min(list.Count, targetLength); i++)
            {
                array[i] = list[i];
            }
            return array;
        }
    }
}