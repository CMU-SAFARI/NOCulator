using System;
using System.Collections.Generic;
using System.Text;

namespace ICSimulator
{
    /*
      Implements controller logic for Golden Packet and Magic Flit.

      Rules are:
        1. every so often, a new epoch beings.
        2. In each epoch, a particular (cpu,mshr) pair is chosen.
        3. There is a magic flit (or several) in the network. A magic flit
           either is a carrier or is not. If it is a carrier and it is ejected,
           the ejecting node must re-inject it on the next cycle, either as
           a carrier (with the next flit from the inj queue) or as an empty
           magic flit.
        4. A magic flit that is not a carrier can have its destination changed.
           (In our implementation, the controller is queried each cycle for
           the dest, but this need not be the case.)
       
       Prioritization:
         beginning of epoch (Magic Flit phase): the Magic Flit (i) should not
           be a carrier, if epoch was long enough (throw warning, count as
           stat if so); (ii) if not, gets a new destination, which is the cpu
           currently favored by GP. This gives an injection guarantee to that
           cpu once the flit gets there.

           (highest to lowest):
           * Magic Flit
           * GP-favored (cpu,mshr), next GP-favored, next next GP-favored, ...
           * everything else

         second half of epoch (GP phase):
           (highest to lowest)
           * GP-favored (cpu,mshr), next, next next, ...
           * everything else
           * Magic Flit (it just bounces around)


        OR: separate GP, MF

          GP works as before: each epoch favors one (cpu,mshr)

          MF works like: MFs are carriers, placeholders or rescuers. A carrier
          has highest priority in the network, then a rescuer, then everything
          else; a placeholder has the lowest priority and no destination, but
          just bounces around. A global coordination mechanism (a) lets the
              controller know when the MF is a placeholder, (b) allows the
              controller to flip the placeholder to a rescuer and set its
              destination. When a rescuer gets to its destination, it can
              absorb the next flit to be injected, become a carrier, and travel
              to that flit's destination. When the carrier is ejected, it
              becomes a placeholder and is re-injected (which is always
              possible when an ejection happens).
      */

    public class Golden
    {
        int m_epochLen;       // epoch len (scaled from L)
        int m_nRescuers;

        int m_goldenPeriod;
        public int GoldenPeriod { get { return m_goldenPeriod; } }

        IEnumerator<int> m_gs; // sequencer
        int m_ticksLeft;

        public Golden()
        {
            int L = Config.network_nrX + Config.network_nrY;
          
            // TODO: scale L when rescuers?

            m_epochLen = (int)(Config.gp_epoch * L);
            
            if (Config.resubmitBuffer && Config.redirection)
                m_epochLen = m_epochLen * 2 + (int)Config.sizeOfRSBuffer * ((int)Config.redirection_threshold + 1);
            
            m_nRescuers = Config.gp_rescuers;

            int n = Simulator.network.injectFlits(m_nRescuers, delegate()
                    {
                        Flit f = new Flit(null, 0);
                        f.state = Flit.State.Placeholder;
                        return f;
                    });

            if (n < m_nRescuers)
                throw new Exception("was not able to initialize all rescuers");
        }

        public void doStep()
        {
            if (m_ticksLeft > 0)
                m_ticksLeft--;
            else
                m_ticksLeft = goldenSequencer();
        }

        // changes state, and returns number of cycles until next move
        int goldenSequencer()
        {
            if (m_gs == null)
                m_gs = goldenSequence().GetEnumerator();

            else if(!m_gs.MoveNext())
                m_gs = goldenSequence().GetEnumerator();

            return m_gs.Current;
        }

        IEnumerable<int> goldenSequence()
        {
            for (int period = 0; period < (Config.mshrs*Config.N); period++)
            {
                m_goldenPeriod = period;
                Coord rescue_dest = new Coord(m_goldenPeriod % Config.N);
                rescue(rescue_dest);

                yield return m_epochLen;
            }
        }

        void rescue(Coord dest)
        {
            int count = 0;

            if (Config.gp_rescuers_dummy) return;

            Simulator.network.visitFlits(delegate(Flit f)
                    {
                        if (f.state == Flit.State.Placeholder)
                        {
                            f.state = Flit.State.Rescuer;
                            f.rescuerCoord = dest;
                            count++;
                        }
                        else if (
                            f.state == Flit.State.Rescuer ||
                            f.state == Flit.State.Carrier)
                            count++;
                    });

            if (count != m_nRescuers)
                throw new Exception(
                        String.Format("could not find all rescuers! count was {0}, nRescuers is {1}",
                            count, m_nRescuers));
        }

        protected void getFlitInfo(Flit f, out int node_mshr)
        {
            node_mshr = -1;

            if (f.packet == null) return;

            int mshr = (f.packet.request != null) ? f.packet.request.mshr : 0;
            int cpu;
            if (f.packet.request != null)
                cpu = f.packet.request.requesterID;
            else
                cpu = f.packet.src.ID;

            node_mshr = (mshr * Config.N + cpu);
        }

        public bool isGolden(Flit f)
        {
            return goldenLevel(f) < Config.gp_levels;
        }

        public bool isSilver(Flit f)
        {
            return goldenLevel(f) == Config.gp_levels;
        }

        public int goldenLevel(Flit f)
        {
            int node_mshr;
            getFlitInfo(f, out node_mshr);
            int lvl = (node_mshr - m_goldenPeriod);
            if (lvl < 0) lvl += (Config.mshrs * Config.N);
            lvl %= (Config.mshrs * Config.N);
            return lvl;
        }
    }
} 
