using Redbox.HAL.Component.Model;
using Redbox.HAL.Component.Model.Extensions;
using System;
using System.Collections.Generic;
using System.Xml;

namespace Redbox.HAL.Controller.Framework
{
    public sealed class Deck : IDeck
    {
        internal const int DefaultSlot1Offset = 780;
        private const int SlotsPerQuadrantDenseDeck = 15;
        private const int SlotsPerQuadrantSparseDeck = 6;
        private readonly List<IQuadrant> m_quadrants = new List<IQuadrant>();

        public override int GetHashCode() => this.Number;

        public override bool Equals(object obj)
        {
            Deck deck = obj as Deck;
            return (object)deck != null && deck == this;
        }

        public static bool operator !=(Deck lhs, Deck rhs) => !(lhs == rhs);

        public static bool operator ==(Deck lhs, Deck rhs)
        {
            if ((object)lhs == (object)rhs)
                return true;
            if ((object)lhs == null || (object)rhs == null || lhs.YOffset != rhs.YOffset || lhs.NumberOfSlots != rhs.NumberOfSlots || lhs.IsQlm != rhs.IsQlm || lhs.SlotWidth != rhs.SlotWidth)
                return false;
            int? sellThruOffset1 = lhs.SellThruOffset;
            int? sellThruOffset2 = rhs.SellThruOffset;
            if (!(sellThruOffset1.GetValueOrDefault() == sellThruOffset2.GetValueOrDefault() & sellThruOffset1.HasValue == sellThruOffset2.HasValue) || lhs.Quadrants.Count != rhs.Quadrants.Count)
                return false;
            for (int index = 0; index < lhs.Quadrants.Count; ++index)
            {
                IQuadrant quadrant1 = lhs.Quadrants[index];
                IQuadrant quadrant2 = rhs.Quadrants[index];
                if (quadrant1.Offset != quadrant2.Offset || quadrant1.IsExcluded != quadrant2.IsExcluded)
                    return false;
            }
            return true;
        }

        public bool IsSlotSellThru(int slot)
        {
            return this.SellThruSlots.HasValue && slot % this.SellThruSlots.Value == 0;
        }

        public int GetSlotOffset(int slot)
        {
            if (!this.IsSlotValid(slot))
                throw new ArgumentException(string.Format("The slot parameter must be between 1 and {0}.", (object)this.NumberOfSlots));
            int num1 = slot - 1;
            int index = num1 / this.SlotsPerQuadrant;
            if (index >= this.Quadrants.Count)
                throw new ArgumentException(string.Format("The computed quadrant offset {0} is outside the range of defined quadrants.  This usuallys means the deck is incorrectly configured.", (object)index));
            int offset = this.Quadrants[index].Offset;
            IQuadrant thatContainsSlot = this.FindQuadrantThatContainsSlot(slot);
            int num2 = num1 % this.SlotsPerQuadrant;
            if (thatContainsSlot != null)
            {
                num2 = slot - thatContainsSlot.Slots.Start;
                offset = thatContainsSlot.Offset;
            }
            Decimal num3 = (Decimal)num2 * this.SlotWidth;
            if (this.IsSlotSellThru(slot))
                num3 = (Decimal)(this.SellThruOffset ?? 915);
            return (int)((Decimal)offset + num3);
        }

        public bool IsSlotValid(int slot) => slot >= 1 && slot <= this.NumberOfSlots;

        public bool IsSparse => this.NumberOfSlots == 72;

        public int Number { get; private set; }

        public int YOffset { get; private set; }

        public bool IsQlm { get; private set; }

        public int NumberOfSlots { get; private set; }

        public Decimal SlotWidth { get; private set; }

        public int? SellThruSlots { get; private set; }

        public int? SellThruOffset { get; private set; }

        public int SlotsPerQuadrant { get; private set; }

        public List<IQuadrant> Quadrants => this.m_quadrants;

        internal Deck(
          int number,
          int yoffset,
          bool isQlm,
          int numberOfSlots,
          Decimal slotWidth,
          int? sellThruSlots,
          int? sellThruOffset,
          Quadrant[] quadrantOffsets)
        {
            this.Number = number;
            this.YOffset = yoffset;
            this.IsQlm = isQlm;
            this.SlotWidth = slotWidth;
            this.SellThruSlots = sellThruSlots;
            this.SellThruOffset = sellThruOffset;
            this.NumberOfSlots = numberOfSlots;
            if (quadrantOffsets != null)
                this.m_quadrants.AddRange((IEnumerable<IQuadrant>)quadrantOffsets);
            else
                this.m_quadrants.Add((IQuadrant)new Quadrant(780));
            this.SlotsPerQuadrant = this.NumberOfSlots / this.Quadrants.Count;
        }

        internal static Deck FromXmlNode(XmlNode node)
        {
            int attributeValue1 = node.GetAttributeValue<int>("Number", 1);
            int attributeValue2 = node.GetAttributeValue<int>("Offset", -18760);
            bool attributeValue3 = node.GetAttributeValue<bool>("IsQlm", false);
            Decimal attributeValue4 = node.GetAttributeValue<Decimal>("SlotWidth", 166.6667M);
            int attributeValue5 = node.GetAttributeValue<int>("NumberOfSlots", 90);
            int? attributeValue6 = node.GetAttributeValue<int?>("SellThruSlots", new int?());
            int? attributeValue7 = node.GetAttributeValue<int?>("SellThruOffset", new int?());
            List<Quadrant> quadrantList = new List<Quadrant>();
            int num1 = 0;
            foreach (XmlNode childNode in node.ChildNodes)
            {
                int attributeValue8 = childNode.GetAttributeValue<int>("Offset", 780);
                int? attributeValue9 = childNode.GetAttributeValue<int?>("StartSlot", new int?());
                int? attributeValue10 = childNode.GetAttributeValue<int?>("EndSlot", new int?());
                bool attributeValue11 = childNode.GetAttributeValue<bool>("IsExcluded", false);
                Range slots;
                if (attributeValue9.HasValue && attributeValue10.HasValue)
                {
                    slots = new Range(attributeValue9.Value, attributeValue10.Value);
                }
                else
                {
                    int num2 = 90 == attributeValue5 ? 15 : 6;
                    int start = num1 * num2 + 1;
                    slots = new Range(start, start + num2 - 1);
                }
                Quadrant quadrant = new Quadrant(attributeValue8, (IRange<int>)slots);
                quadrantList.Add(quadrant);
                quadrant.IsExcluded = attributeValue11;
                ++num1;
            }
            return new Deck(attributeValue1, attributeValue2, attributeValue3, attributeValue5, attributeValue4, attributeValue6, attributeValue7, quadrantList.ToArray());
        }

        internal void ToXmlWriter(XmlWriter writer)
        {
            writer.WriteStartElement(nameof(Deck));
            writer.WriteAttributeString("Number", XmlConvert.ToString(this.Number));
            writer.WriteAttributeString("Offset", XmlConvert.ToString(this.YOffset));
            writer.WriteAttributeString("IsQlm", XmlConvert.ToString(this.IsQlm));
            writer.WriteAttributeString("SlotWidth", XmlConvert.ToString(this.SlotWidth));
            writer.WriteAttributeString("NumberOfSlots", XmlConvert.ToString(this.NumberOfSlots));
            if (this.SellThruSlots.HasValue)
                writer.WriteAttributeString("SellThruSlots", XmlConvert.ToString(this.SellThruSlots.Value));
            int? sellThruOffset = this.SellThruOffset;
            if (sellThruOffset.HasValue)
            {
                XmlWriter xmlWriter = writer;
                sellThruOffset = this.SellThruOffset;
                string str = XmlConvert.ToString(sellThruOffset.Value);
                xmlWriter.WriteAttributeString("SellThruOffset", str);
            }
            foreach (IQuadrant quadrant in this.Quadrants)
            {
                writer.WriteStartElement("Quadrant");
                writer.WriteAttributeString("Offset", XmlConvert.ToString(quadrant.Offset));
                if (quadrant.Slots != null)
                {
                    writer.WriteAttributeString("StartSlot", XmlConvert.ToString(quadrant.Slots.Start));
                    writer.WriteAttributeString("EndSlot", XmlConvert.ToString(quadrant.Slots.End));
                }
                if (quadrant.IsExcluded)
                    writer.WriteAttributeString("IsExcluded", XmlConvert.ToString(true));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        internal void UpdateFrom(Deck newProperties)
        {
            this.YOffset = newProperties.YOffset;
            this.IsQlm = newProperties.IsQlm;
            this.SlotWidth = newProperties.SlotWidth;
            this.SellThruOffset = newProperties.SellThruOffset;
            int numberOfSlots = this.NumberOfSlots;
            this.NumberOfSlots = newProperties.NumberOfSlots;
            this.m_quadrants.Clear();
            this.m_quadrants.AddRange((IEnumerable<IQuadrant>)newProperties.Quadrants);
        }

        private IQuadrant FindQuadrantThatContainsSlot(int slot)
        {
            foreach (IQuadrant quadrant in this.Quadrants)
            {
                if (quadrant.Slots == null)
                    return (IQuadrant)null;
                if (quadrant.Slots.Includes(slot))
                    return quadrant;
            }
            return (IQuadrant)null;
        }
    }
}
