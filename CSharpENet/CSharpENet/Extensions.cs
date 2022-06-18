namespace ENet;

static class LinkedListExtension
{
    static public void AddLastRange<T>(this LinkedList<T> list, LinkedListNode<T> startNode, LinkedListNode<T>? endNode)
    {
        LinkedListNode<T>? currNode = startNode;

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
