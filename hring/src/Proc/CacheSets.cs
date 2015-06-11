using System;
using System.Collections.Generic;

namespace ICSimulator
{
    public class CacheSets<Block>
    {
        class Set
        {
            public ulong line;
            public Block block;
            public Set prev, next;
        }

        Set[] m_sets;
        int m_setcount, m_waycount;

        ulong m_set_mask;

        public CacheSets(int size, int linesize, int ways)
        {
            m_setcount = 1 << (size - linesize - ways);
            m_waycount = 1 << ways;
 
            m_set_mask = (ulong)m_setcount - 1;

            m_sets = new Set[m_setcount];
            for (int i = 0; i < m_setcount; i++)
            {
                Set prev = null;
                Set first = null, last = null;
                for (int j = 0; j < m_waycount; j++)
                {
                    Set s = new Set();
                    s.line = ulong.MaxValue;
                    s.prev = prev;
                    if (prev != null) prev.next = s;
                    if (prev == null) first = s;
                    if (j == m_waycount - 1) last = s;
                    prev = s;
                }
                last.next = first;
                first.prev = last;

                m_sets[i] = first;
            }
        }

        // places a block in the cache, returning the block that was evicted
        // (or null)
        public Block replace(ulong line, Block block)
        {
            // look up set
            Set s = getSet(line);
            Set first = s;

            // test to see if already in the set
            int i;
            for (i = 0; i < m_waycount; i++, s = s.next)
                if (s.line == line)
                    break;

            if (i < m_waycount)
                // found: move to head of LRU queue
            {
                s.prev.next = s.next; // remove from circular list
                s.next.prev = s.prev;
                if (s == first) first = s.next;

                s.next = first; // insert before rest of list
                s.prev = first.prev;
                s.next.prev = s;
                s.prev.next = s;

                setSet(line, s);

                return default(Block);
            }
            else
            {
                // evict old line
                Set evict = first.prev; // least-recently used
                // move to head (just rotate in, since LRU queue is circular)
                setSet(line, evict);

                // set new block
                Block evictBlock = evict.block;
                evict.line = line;
                evict.block = block;

                return evictBlock;
            }
        }

        public Block find(ulong line)
        {
            Set s = getSet(line);
            for (int i = 0; i < m_waycount; i++, s = s.next)
                if (line == s.line)
                    return s.block;

            return default(Block);
        }

        Set getSet(ulong line)
        {
            ulong set = line & m_set_mask;
            return m_sets[set];
        }

        void setSet(ulong line, Set s)
        {
            ulong set = line & m_set_mask;
            m_sets[set] = s;
        }

        public int WarmBlocks
        {
            get
            {
                int count = 0;

                for (int i = 0; i < m_setcount; i++)
                {
                    Set s = m_sets[i];
                    for (int j = 0; j < m_waycount; j++, s = s.next)
                        if (s.block != null) count++;
                }

                return count;
            }
        }

        public int TotalBlocks
        { get { return m_setcount * m_waycount; } }
    }
}
