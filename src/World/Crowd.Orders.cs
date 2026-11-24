// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClaudeTogether
{
    /// <summary>A meal that a worker has ordered on the wall phone: the courier comes to the front door, the worker takes it to the kitchen.</summary>
    sealed class Order
    {
        public string Account;
        public double At;               // the courier comes at this time (Crowd.Time)
        public Npc Courier;
        public int State;               // 0 waiting, 1 both on their way, 2 handing it over
        public double Timer;
    }

    // Phone, courier, meal; and the line for the toilet cabins, where waiting too long turns a worker red.
    sealed partial class Crowd
    {
        public readonly List<Order> Orders = new List<Order>();
        Spot pickupDoor;                // where the courier stands, just inside the front door
        int courierCount;

        /// <summary>Two places in front of the front door: the courier's and the worker's, facing each other.</summary>
        void AddPickupSpots()
        {
            if (entrance == null || upperLayout == null) return;
            int tx = entrance.X;
            Spot c = new Spot();
            c.Level = 0; c.Group = "pickup"; c.Id = "courier"; c.Dynamic = true; c.Tx = tx; c.Ty = 1; c.Fx = tx + 0.5; c.Fy = 1.15; c.Face = Facing.Down; c.Pose = Pose.Stand; c.Min = c.Max = 1e6;
            pickupDoor = c;
            Spot w = new Spot();
            w.Level = 0; w.Group = "pickup"; w.Id = "pickup"; w.Tx = tx; w.Ty = 2; w.Fx = tx + 0.5; w.Fy = 2.05; w.Face = Facing.Up; w.Pose = Pose.Stand; w.Min = w.Max = 1e6;
            errands.Add(w);
        }

        /// <summary>A worker has rung up: the meal comes in half a minute or so (one order per worker, two at a time at most).</summary>
        void ScheduleOrder(Npc n)
        {
            if (pickupDoor == null || Orders.Count >= 2 || Orders.Any(q => q.Account == n.Account)) return;
            Order o = new Order();
            o.Account = n.Account;
            o.At = Time + Between(30, 50);
            Orders.Add(o);
        }

        void EndOrder(Order o)
        {
            Orders.Remove(o);
            Npc c = o.Courier;
            if (c != null && Npcs.Contains(c) && !c.Leaving) { c.HasBox = false; Leave(c); }
            Npc w = Find(o.Account);
            if (w != null && o.State == 1) { w.Wait = 0; if (w.At != null && w.At.Id == "pickup") w.Plan.Clear(); }
        }

        void StepOrders(double seconds)
        {
            foreach (Order o in Orders.ToArray())
            {
                Npc w = Find(o.Account);
                if (w == null || w.Leaving) { EndOrder(o); continue; }
                if (o.State == 0)
                {
                    if (Time < o.At) continue;
                    // the courier is at the door; the worker goes to it as soon as it is free
                    if (w.Plan.Count > 0 || w.Walking || w.Riding || w.Meeting != null || w.Praying || w.HasCue || w.Queueing || w.Level != 0 || (w.Goal != null && w.Goal.Inside)) continue;
                    Npc c = new Npc();
                    c.Account = "\u0001courier" + (++courierCount);
                    c.Courier = true; c.HasBox = true; c.Style = 909; c.Face = Facing.Down;
                    c.X = entrance.X + 0.5; c.Y = entrance.Y + 0.3;
                    Npcs.Add(c);
                    byName[c.Account] = c;
                    if (!GoTo(c, pickupDoor)) { Npcs.Remove(c); byName.Remove(c.Account); Orders.Remove(o); continue; }
                    w.Plan.Clear();
                    w.Plan.Add("pickup"); w.Plan.Add("table"); w.Plan.Add("ksink"); w.Plan.Add("desk");
                    w.Delivery = true;
                    w.Dozing = false;
                    w.Wait = 0;
                    o.Courier = c;
                    o.State = 1;
                    continue;
                }
                o.Timer += seconds;
                if (o.State == 1)
                {
                    if (o.Timer > 120) { EndOrder(o); continue; }
                    Npc c = o.Courier;
                    bool courierThere = c != null && Npcs.Contains(c) && !c.Walking && c.At == pickupDoor;
                    bool workerThere = !w.Walking && !w.Riding && w.At != null && w.At.Id == "pickup";
                    if (courierThere && workerThere) { o.State = 2; o.Timer = 0; c.Face = Facing.Down; w.Face = Facing.Up; }
                    continue;
                }
                if (o.Timer >= 1.6)                                    // the bag changes hands, the courier goes, the worker goes on
                {
                    w.HasFood = true;
                    w.Wait = 0;
                    if (o.Courier != null) o.Courier.HasBox = false;
                    EndOrder(o);
                }
            }
        }

        void StepCourier(Npc n, double seconds, Action<string> use)
        {
            if (n.Way != null) { Walk(n, seconds, use); return; }
            if (n.Leaving) Remove(n);
        }

        // ---------- the line for the toilets ----------

        Spot[] queueSpots;

        /// <summary>Places in the hall in front of the cabins where workers wait when every cabin is taken.</summary>
        Spot[] QueueSpots()
        {
            if (queueSpots != null) return queueSpots;
            List<Spot> list = new List<Spot>();
            for (int x = 19; x <= 24; x++)
            {
                if (!layout.Walkable(x, 8)) continue;
                Spot s = new Spot();
                s.Level = 0; s.Group = "queue"; s.Id = "queue"; s.Dynamic = true; s.Tx = x; s.Ty = 8; s.Fx = x + 0.5; s.Fy = 8.55; s.Face = Facing.Up; s.Pose = Pose.Stand; s.Min = s.Max = 1e6;
                list.Add(s);
            }
            queueSpots = list.ToArray();
            return queueSpots;
        }

        /// <summary>Every cabin is taken: the worker goes and waits in the hall (if there is room in the line).</summary>
        bool TryQueue(Npc n)
        {
            if (upperErrands == null) return false;                            // the line is part of the building with two floors
            foreach (Spot s in QueueSpots())
            {
                if (Taken(s, n)) continue;
                if (!GoTo(n, s)) return false;
                n.Queueing = true;
                n.QueuedFor = 0;
                return true;
            }
            return false;
        }

        /// <summary>Waiting: the face goes red after a while; as soon as a cabin is free the worker goes in. True while it keeps waiting.</summary>
        bool StepQueue(Npc n, double seconds)
        {
            if (n.Presence == Presence.Busy) { n.Queueing = false; n.Plan.Clear(); return false; }
            n.QueuedFor += seconds;
            n.Blush = Math.Max(0.0, Math.Min(1.0, (n.QueuedFor - 10.0) / 20.0));
            if (Resolve(n, "cabin", null) != null) { n.Queueing = false; n.Wait = 0; return false; }
            return true;
        }
    }
}
