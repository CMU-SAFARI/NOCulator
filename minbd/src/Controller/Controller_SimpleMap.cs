using System;
using System.Collections.Generic;
using System.IO;

namespace ICSimulator
{
    class Controller_SimpleMap : Controller_ClassicBLESS
    {
        double[] lambdas;
        double[] misses;

        public Controller_SimpleMap()
        {
            lambdas = parseListParam(Config.lambdas, Config.simplemap_lambda);
            misses = parseListParam(Config.misses, 0.0);
        }

        double[] parseListParam(string param, double defl)
        {
            double[] ret = new double[Config.N];

            if (param != "")
            {
                // can specify '=filename' to read lambdas from a file corresponding to workload file
                if (param[0] == '=' && Config.workload_number > 0)
                    param = File.ReadAllLines(param.Substring(1))[Config.workload_number - 1];

                int i = 0;
                foreach (string part in param.Split(','))
                    ret[i++] = Double.Parse(part);
            }
            else
            {
                for (int i = 0; i < Config.N; i++)
                    ret[i] = defl;
            }

            return ret;
        }

        public override int mapCache(int node, ulong block)
        {
            int cx, cy;
            Coord.getXYfromID(node, out cx, out cy);

            if (Config.overlapping_squares != -1)
            {
                // stripe based on block
                int stripe = (int)(block % (ulong)(Config.overlapping_squares * Config.overlapping_squares));
               
                int nx = cx - Config.overlapping_squares/2 + (stripe % Config.overlapping_squares),
                    ny = cy - Config.overlapping_squares/2 + (stripe / Config.overlapping_squares);

                if (nx < 0) nx = -nx;
                if (nx >= Config.network_nrX) nx = Config.network_nrX - nx;
                if (ny < 0) ny = -ny;
                if (ny >= Config.network_nrY) ny = Config.network_nrY - ny;

                return Coord.getIDfromXY(nx, ny);
            }

            if (Config.neighborhood_locality != -1)
            {
                // get top-left corner of neighborhood
                int neigh_x = cx - (cx % Config.neighborhood_locality), neigh_y = cy - (cy % Config.neighborhood_locality);

                // stripe based on block
                int stripe = (int)(block % (ulong)(Config.neighborhood_locality * Config.neighborhood_locality));

                int nx = neigh_x + (stripe % Config.neighborhood_locality);
                int ny = neigh_y + (stripe / Config.neighborhood_locality);

                return Coord.getIDfromXY(nx, ny);
            }

            double dist = 0;

            if(Config.bounded_locality != -1) {
                dist = Math.Ceiling(Simulator.rand.NextDouble() * Config.bounded_locality);
            } else {
                // simple exponential with lambda either Config.simplemap_lambda or per-node lambda from Config.lambdas
                // quantile function is F(lambda,p) = -ln(1-p) / lambda
                dist = - Math.Log(1 - Simulator.rand.NextDouble()) / lambdas[node];
            }

            double angle = 2 * Math.PI * Simulator.rand.NextDouble();

            double x = dist * Math.Cos(angle);
            double y = dist * Math.Sin(angle);

            if (Config.torus)
            {
                if (x >= Config.network_nrX/2) x = Config.network_nrX/2;
                if (x <= -Config.network_nrX/2) x = -Config.network_nrX/2;
                if (y >= Config.network_nrY/2) y = Config.network_nrY/2;
                if (y <= -Config.network_nrY/2) y = -Config.network_nrY/2;
                cx = (int)(cx + x);
                cy = (int)(cy + y);
                if (cx < 0) cx += Config.network_nrX;
                if (cx >= Config.network_nrX) cx -= Config.network_nrX;
                if (cy < 0) cy += Config.network_nrY;
                if (cy >= Config.network_nrY) cy -= Config.network_nrY;
                return Coord.getIDfromXY(cx, cy);
            }
            else
            {
                if ((int)(cx + x) >= Config.network_nrX) x = -x;
                if ((int)(cx + x) < 0) x = -x;
                if ((int)(cy + y) >= Config.network_nrY) y = -y;
                if ((int)(cy + y) < 0) y = -y;
                cx = (int)(cx + x);
                cy = (int)(cy + y);
                if (cx < 0) cx = 0;
                if (cx >= Config.network_nrX) cx = Config.network_nrX - 1;
                if (cy < 0) cy = 0;
                if (cy >= Config.network_nrY) cy = Config.network_nrY - 1;
                return Coord.getIDfromXY(cx, cy);
            }
        }

        public override int mapMC(int node, ulong block)
        {
            // TODO: sparser MC mappings? for now, just assume same node as shared cache slice
            return mapCache(node, block);
        }

        ulong[] MCreadyCycles = new ulong[Config.N];

        public override int memMiss(int node, ulong block)
        {
            if (!Config.simplemap_mem) return -1;

            bool isMiss = Simulator.rand.NextDouble() < misses[node];

            if (isMiss)
            {
                int MCnode = mapMC(node, block);
                if (MCreadyCycles[MCnode] < Simulator.CurrentRound + (ulong)Config.simple_MC_think)
                    MCreadyCycles[MCnode] = Simulator.CurrentRound + (ulong)Config.simple_MC_think;

                int lat = (int)(MCreadyCycles[MCnode] - Simulator.CurrentRound);
                MCreadyCycles[MCnode] += (ulong)Config.simple_MC_think;
                return lat;
            }
            else
                return -1;
        }
    }
}
