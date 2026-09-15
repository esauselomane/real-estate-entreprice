namespace Identity.Microservice.Controllers;

/// <summary>Resolves an agency and its descendants, retaining the root and tolerating hierarchy cycles.</summary>
public static class AgencyTenantScope
{
    public static List<string> GetSubtree(string rootTenantId, IReadOnlyDictionary<string, List<string>> childrenByParent)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootTenantId };
        var pending = new Queue<string>();
        pending.Enqueue(rootTenantId);
        while (pending.Count > 0)
        {
            var parentTenantId = pending.Dequeue();
            if (!childrenByParent.TryGetValue(parentTenantId, out var children)) continue;
            foreach (var childTenantId in children)
            {
                if (visited.Add(childTenantId)) pending.Enqueue(childTenantId);
            }
        }
        return visited.ToList();
    }
}
