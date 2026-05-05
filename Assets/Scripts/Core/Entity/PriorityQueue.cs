using System;
using System.Collections.Generic;

/// <summary>
/// 基于最小堆（Min-Heap）的高性能优先队列
/// 适用于 A*、Dijkstra 等需要频繁弹出最小代价值的寻路算法
/// </summary>
public class PriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
{
    // 使用结构体避免内部节点产生 GC 堆分配
    private struct Node
    {
        public TElement Element;
        public TPriority Priority;
    }

    // 内部数组，动态扩容
    private readonly List<Node> _elements;

    public PriorityQueue(int initialCapacity = 64)
    {
        _elements = new List<Node>(initialCapacity);
    }

    /// <summary>
    /// 队列中当前的元素数量
    /// </summary>
    public int Count => _elements.Count;

    /// <summary>
    /// 入队：将元素压入堆尾，并执行上浮（Bubble Up）
    /// </summary>
    public void Enqueue(TElement element, TPriority priority)
    {
        _elements.Add(new Node { Element = element, Priority = priority });
        BubbleUp(_elements.Count - 1);
    }

    /// <summary>
    /// 出队：弹出堆顶（优先级最小/最高）的元素，将末尾元素移至堆顶并下沉（Bubble Down）
    /// </summary>
    public TElement Dequeue()
    {
        if (_elements.Count == 0)
            throw new InvalidOperationException("Queue is empty.");

        // 记录堆顶元素（优先级数值最小的元素）
        TElement result = _elements[0].Element;
        
        int lastIndex = _elements.Count - 1;

        // 将堆尾元素移动到堆顶，并移除堆尾
        _elements[0] = _elements[lastIndex];
        _elements.RemoveAt(lastIndex);

        // 如果还有元素，执行下沉以维持最小堆特性
        if (_elements.Count > 0)
        {
            BubbleDown(0);
        }

        return result;
    }

    /// <summary>
    /// 清空队列（保留内部数组容量，避免下次寻路重新分配内存产生 GC）
    /// </summary>
    public void Clear()
    {
        _elements.Clear();
    }

    // --- 私有核心堆操作算法 ---

    private void BubbleUp(int index)
    {
        while (index > 0)
        {
            int parentIndex = (index - 1) / 2;

            // 如果当前节点 >= 父节点，说明符合最小堆结构，上浮结束
            if (_elements[index].Priority.CompareTo(_elements[parentIndex].Priority) >= 0)
                break;

            // 否则，交换它们并继续上浮
            Swap(index, parentIndex);
            index = parentIndex;
        }
    }

    private void BubbleDown(int index)
    {
        int lastIndex = _elements.Count - 1;
        while (true)
        {
            int leftChildIndex = index * 2 + 1;
            int rightChildIndex = index * 2 + 2;
            int smallestIndex = index;

            // 检查左子节点是否更小
            if (leftChildIndex <= lastIndex && 
                _elements[leftChildIndex].Priority.CompareTo(_elements[smallestIndex].Priority) < 0)
            {
                smallestIndex = leftChildIndex;
            }

            // 检查右子节点是否更小
            if (rightChildIndex <= lastIndex && 
                _elements[rightChildIndex].Priority.CompareTo(_elements[smallestIndex].Priority) < 0)
            {
                smallestIndex = rightChildIndex;
            }

            // 如果自己就是最小的，下沉结束
            if (smallestIndex == index)
                break;

            // 否则，与最小的子节点交换，并继续下沉
            Swap(index, smallestIndex);
            index = smallestIndex;
        }
    }

    private void Swap(int i, int j)
    {
        Node temp = _elements[i];
        _elements[i] = _elements[j];
        _elements[j] = temp;
    }
}