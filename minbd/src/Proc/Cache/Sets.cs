using System;

class Sets<T>
{
    int m_shift;
    int m_ways, m_sets;
    bool[] m_valid;
    ulong[] m_last;
    ulong[] m_tags;
    T[] m_data;

    int idx(int w, int s) { return s*m_ways + w; }

    public Sets(int shift, int ways, int sets)
    {
        m_shift = shift;
        m_ways = ways;
        m_sets = sets;

        m_valid = new bool[ways * sets];
        m_last = new ulong[ways * sets];
        m_tags = new ulong[ways * sets];
        m_data = new T[ways * sets];

        for (int i = 0; i < ways*sets; i++)
        {
            m_valid[i] = false;
            m_last[i] = 0;
            m_tags[i] = 0;
        }
    }

    // probes for address. returns 'true' if found. data returned in 'data'.
    public bool probe(ulong addr, out T data)
    {
        ulong block = addr >> m_shift;
        int set_idx = (int)(block % (ulong)m_sets);

        data = default(T);

        for (int way = 0; way < m_ways; way++)
        {
            int i = idx(way, set_idx);

            if (m_valid[i] && m_tags[i] == block)
            {
                data = m_data[i];
                return true;
            }
        }

        return false;
    }

    public bool setdata(ulong addr, T data)
    {
        ulong block = addr >> m_shift;
        int set_idx = (int)(block % (ulong)m_sets);

        for (int way = 0; way < m_ways; way++)
        {
            int i = idx(way, set_idx);

            if (m_valid[i] && m_tags[i] == block)
            {
                m_data[i] = data;
                return true;
            }
        }

        return false;
    }

    // updates last-used timestamp on address. returns 'true' if found.
    public bool update(ulong addr, ulong cyc)
    {
        ulong block = addr >> m_shift;
        int set_idx = (int)(block % (ulong)m_sets);

        for (int way = 0; way < m_ways; way++)
        {
            int i = idx(way, set_idx);

            if (m_valid[i] && m_tags[i] == block)
            {
                m_last[i] = cyc;
                return true;
            }
        }

        return false;
    }

    // inserts cache block, possibly evicts old one. returns 'true'
    // if block was evicted. evicted data returned in 'evicted'.
    public bool insert(ulong addr, T data, out ulong evict_addr, out T evicted, ulong cyc)
    {
        ulong block = addr >> m_shift;
        int set_idx = (int)(block % (ulong)m_sets);

        int replace_idx = -1;
        ulong lru_cyc = 0;

        for (int way = 0; way < m_ways; way++)
        {
            int i = idx(way, set_idx);

            if (!m_valid[i])
            {
                replace_idx = i;
                break;
            }

            if (replace_idx == -1 || m_last[i] <= lru_cyc)
            {
                replace_idx = i;
                lru_cyc = m_last[i];
            }
        }

        bool have_evicted = m_valid[replace_idx];
        if (have_evicted)
        {
            evict_addr = m_tags[replace_idx] << m_shift;
            evicted = m_data[replace_idx];
        }
        else
        {
            evict_addr = 0;
            evicted = default(T);
        }

        m_valid[replace_idx] = true;
        m_last[replace_idx] = cyc;
        m_tags[replace_idx] = block;
        m_data[replace_idx] = data;

        return have_evicted;
    }

    public bool inval(ulong addr, out T evicted)
    {
        ulong block = addr >> m_shift;
        int set_idx = (int)(block % (ulong)m_sets);

        evicted = default(T);

        for (int way = 0; way < m_ways; way++)
        {
            int i = idx(way, set_idx);

            if (m_valid[i] && m_tags[i] == block)
            {
                evicted = m_data[i];
                m_valid[i] = false;
                m_tags[i] = 0;
                return true;
            }
        }

        return false;
    }
};
