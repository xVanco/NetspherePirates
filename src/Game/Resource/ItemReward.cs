﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Netsphere.Resource
{
    internal class CapsuleReward
    {
        public ItemNumber Item { get; set; }

        public List<BagReward> Bags { get; set; }
    }

    internal class BagReward
    {
        public List<ItemReward> Bag { get; set; }

        public ItemReward Take()
        {
            var rand = new Random();

            while (true)
            {
                var queryable = (from p in Bag
                                where p.Rate >= rand.Next(100)
                                select p).ToList();

                if (queryable.Any())
                    return queryable.First();
            }
        }
    }

    internal class ItemReward
    {
        public CapsuleRewardType Type { get; set; }

        public ItemNumber Item { get; set; }

        public ItemPriceType PriceType { get; set; }

        public ItemPeriodType PeriodType { get; set; }

        public byte Color { get; set; }

        public uint PEN { get; set; }

        public uint Period { get; set; }

        public uint Effect { get; set; }

        public uint Rate { get; set; }
    }
}
