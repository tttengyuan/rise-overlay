using System.Collections.ObjectModel;

namespace RiseOverlay.UI.ViewModels;

internal static class ObservableCollectionSync
{
    public static void Values<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        int shared = Math.Min(target.Count, source.Count);
        for (int i = 0; i < shared; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(target[i], source[i]))
                target[i] = source[i];
        }

        while (target.Count > source.Count)
            target.RemoveAt(target.Count - 1);
        for (int i = target.Count; i < source.Count; i++)
            target.Add(source[i]);
    }

    public static void Instances<T>(ObservableCollection<T> target, IReadOnlyList<T> desired)
        where T : class
    {
        for (int i = 0; i < desired.Count; i++)
        {
            if (i < target.Count && ReferenceEquals(target[i], desired[i]))
                continue;

            int existingIndex = -1;
            for (int j = i + 1; j < target.Count; j++)
            {
                if (ReferenceEquals(target[j], desired[i]))
                {
                    existingIndex = j;
                    break;
                }
            }

            if (existingIndex >= 0)
                target.Move(existingIndex, i);
            else
                target.Insert(i, desired[i]);
        }

        while (target.Count > desired.Count)
            target.RemoveAt(target.Count - 1);
    }
}
