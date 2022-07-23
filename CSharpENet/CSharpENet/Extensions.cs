namespace ENet;

static class ListExtension
{
    static public void AddLastRange<T>(this List<T> list, ListNode<T> startNode, ListNode<T>? endNode)
    {
        ListNode<T>? currNode = startNode;

        while (currNode != null && !currNode.Equals(endNode))
        {
            list.AddLast(currNode);
            currNode = currNode.Next;
        }

        if (endNode != null)
        {
            list.AddLast(endNode);
        }
    }

}
