namespace Starbind.Combat
{
    /// <summary>Element marks on one target (GDD §5.5): at most two; a new element replaces the older
    /// one; re-applying the same element refreshes it. Pure data, driven by explicit time.</summary>
    public sealed class StatusSet
    {
        public const int MaxMarks = 2;

        struct Mark
        {
            public ElementType Element;
            public float AppliedAt;
            public float ExpiresAt;
        }

        readonly Mark[] marks = new Mark[MaxMarks];

        public int Count
        {
            get
            {
                int n = 0;
                for (int i = 0; i < MaxMarks; i++)
                    if (marks[i].Element != ElementType.None)
                        n++;
                return n;
            }
        }

        public void Apply(ElementType element, float duration, float now)
        {
            if (element == ElementType.None || duration <= 0)
                return;
            Expire(now);
            for (int i = 0; i < MaxMarks; i++)
                if (marks[i].Element == element)
                {
                    marks[i].ExpiresAt = System.Math.Max(marks[i].ExpiresAt, now + duration);
                    return;
                }
            int slot = 0;
            for (int i = 0; i < MaxMarks; i++)
            {
                if (marks[i].Element == ElementType.None) { slot = i; break; }
                if (marks[i].AppliedAt < marks[slot].AppliedAt) slot = i;
            }
            marks[slot] = new Mark { Element = element, AppliedAt = now, ExpiresAt = now + duration };
        }

        public bool Has(ElementType element, float now)
        {
            for (int i = 0; i < MaxMarks; i++)
                if (marks[i].Element == element && marks[i].ExpiresAt > now)
                    return true;
            return false;
        }

        public float Remaining(ElementType element, float now)
        {
            for (int i = 0; i < MaxMarks; i++)
                if (marks[i].Element == element && marks[i].ExpiresAt > now)
                    return marks[i].ExpiresAt - now;
            return 0;
        }

        public void Consume(ElementType element)
        {
            for (int i = 0; i < MaxMarks; i++)
                if (marks[i].Element == element)
                    marks[i] = default;
        }

        public void Expire(float now)
        {
            for (int i = 0; i < MaxMarks; i++)
                if (marks[i].Element != ElementType.None && marks[i].ExpiresAt <= now)
                    marks[i] = default;
        }

        public void Clear()
        {
            for (int i = 0; i < MaxMarks; i++)
                marks[i] = default;
        }
    }
}
