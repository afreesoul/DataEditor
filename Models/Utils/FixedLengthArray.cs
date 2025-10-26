using System;
using System.Collections;
using System.Collections.Generic;

namespace GameDataEditor.Models.Utils
{
    /// <summary>
    /// 固定长度的泛型数组类
    /// 使用List<T>作为后备存储，避免JSON序列化问题
    /// </summary>
    /// <typeparam name="T">数组元素类型</typeparam>
    public class FixedLengthArray<T> : IList<T>, IReadOnlyList<T>, IList
    {
        private readonly List<T> _items;

        /// <summary>
        /// 构造函数，指定数组长度
        /// </summary>
        /// <param name="length">数组长度</param>
        public FixedLengthArray(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "数组长度不能为负数");
                
            _items = new List<T>(length);
            
            // 使用默认值填充列表
            for (int i = 0; i < length; i++)
            {
                _items.Add(default(T)!);
            }
        }

        /// <summary>
        /// 获取数组长度
        /// </summary>
        public int Length => _items.Capacity;

        /// <summary>
        /// 获取或设置指定索引处的元素
        /// </summary>
        /// <param name="index">索引</param>
        /// <returns>元素值</returns>
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _items.Capacity)
                    throw new ArgumentOutOfRangeException(nameof(index));
                
                // 如果索引超出当前列表大小但小于容量，返回默认值
                if (index >= _items.Count)
                    return default(T)!;
                    
                return _items[index];
            }
            set
            {
                if (index < 0 || index >= _items.Capacity)
                    throw new ArgumentOutOfRangeException(nameof(index));
                
                // 确保列表有足够的元素
                while (_items.Count <= index)
                {
                    _items.Add(default(T)!);
                }
                
                _items[index] = value;
            }
        }

        /// <summary>
        /// 获取数组元素数量（与长度相同）
        /// </summary>
        public int Count => _items.Capacity;

        // 显式实现IList的Count属性（非泛型版本）
        int ICollection.Count => Count;

        /// <summary>
        /// 获取指示集合是否为只读的值
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// 将指定索引处的元素设置为其类型的默认值
        /// </summary>
        /// <param name="index">索引</param>
        public void Clear(int index)
        {
            if (index < 0 || index >= _items.Capacity)
                throw new ArgumentOutOfRangeException(nameof(index));
                
            if (index < _items.Count)
                _items[index] = default(T)!;
        }

        /// <summary>
        /// 确定数组中是否包含指定元素
        /// </summary>
        /// <param name="item">要查找的元素</param>
        /// <returns>如果包含则为 true，否则为 false</returns>
        public bool Contains(T item)
        {
            return _items.Contains(item);
        }

        /// <summary>
        /// 将整个数组复制到指定数组中
        /// </summary>
        /// <param name="array">目标数组</param>
        /// <param name="arrayIndex">目标数组中开始复制的位置</param>
        public void CopyTo(T[] array, int arrayIndex)
        {
            for (int i = 0; i < _items.Capacity; i++)
            {
                if (arrayIndex + i >= array.Length) break;
                array[arrayIndex + i] = this[i];
            }
        }

        // ICollection接口的显式实现
        void ICollection.CopyTo(Array array, int arrayIndex)
        {
            for (int i = 0; i < _items.Capacity; i++)
            {
                if (arrayIndex + i >= array.Length) break;
                array.SetValue(this[i], arrayIndex + i);
            }
        }

        // ICollection接口的显式实现
        bool ICollection.IsSynchronized => false;

        // ICollection接口的显式实现
        object ICollection.SyncRoot => this;

        /// <summary>
        /// 返回循环访问数组的枚举器
        /// </summary>
        /// <returns>数组枚举器</returns>
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _items.Capacity; i++)
            {
                yield return this[i];
            }
        }

        /// <summary>
        /// 返回循环访问数组的枚举器
        /// </summary>
        /// <returns>数组枚举器</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 获取指定元素的索引
        /// </summary>
        /// <param name="item">要查找的元素</param>
        /// <returns>元素的索引，如果未找到则为 -1</returns>
        public int IndexOf(T item)
        {
            for (int i = 0; i < _items.Capacity; i++)
            {
                if (Equals(this[i], item))
                    return i;
            }
            return -1;
        }

        // IList接口的显式实现
        int IList.Add(object? value)
        {
            throw new NotSupportedException("固定长度数组不支持添加操作");
        }

        // IList接口的显式实现
        bool IList.Contains(object? value)
        {
            if (value is T item)
                return Contains(item);
            return false;
        }

        // IList接口的显式实现
        int IList.IndexOf(object? value)
        {
            if (value is T item)
                return IndexOf(item);
            return -1;
        }

        // IList接口的显式实现
        void IList.Insert(int index, object? value)
        {
            throw new NotSupportedException("固定长度数组不支持插入操作");
        }

        // IList接口的显式实现
        void IList.Remove(object? value)
        {
            throw new NotSupportedException("固定长度数组不支持移除操作");
        }

        // IList接口的显式实现
        object? IList.this[int index]
        {
            get => this[index];
            set
            {
                if (value is T item)
                    this[index] = item;
                else
                    throw new ArgumentException("值类型不匹配");
            }
        }

        // IList接口的显式实现
        bool IList.IsFixedSize => true;

        // IList接口的显式实现
        bool IList.IsReadOnly => false;

        // 以下方法在固定长度数组中不支持，因为会改变数组长度
        
        /// <summary>
        /// 不支持的操作 - 无法在固定长度数组中添加元素
        /// </summary>
        public void Add(T item)
        {
            throw new NotSupportedException("固定长度数组不支持添加操作");
        }

        /// <summary>
        /// 不支持的操作 - 无法在固定长度数组中清除所有元素
        /// </summary>
        public void Clear()
        {
            throw new NotSupportedException("固定长度数组不支持清除操作");
        }

        /// <summary>
        /// 不支持的操作 - 无法在固定长度数组中插入元素
        /// </summary>
        public void Insert(int index, T item)
        {
            throw new NotSupportedException("固定长度数组不支持插入操作");
        }

        /// <summary>
        /// 不支持的操作 - 无法在固定长度数组中移除元素
        /// </summary>
        public bool Remove(T item)
        {
            throw new NotSupportedException("固定长度数组不支持移除操作");
        }

        /// <summary>
        /// 不支持的操作 - 无法在固定长度数组中按索引移除元素
        /// </summary>
        public void RemoveAt(int index)
        {
            throw new NotSupportedException("固定长度数组不支持移除操作");
        }

        /// <summary>
        /// 返回表示当前数组的字符串
        /// </summary>
        /// <returns>数组内容的字符串表示</returns>
        public override string ToString()
        {
            var items = new List<string>();
            for (int i = 0; i < _items.Capacity; i++)
            {
                var item = this[i];
                items.Add(item?.ToString() ?? "null");
            }
            return $"[{string.Join(", ", items)}]";
        }
    }
}