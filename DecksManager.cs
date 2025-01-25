using Redbox.HAL.Component.Model;
using Redbox.HAL.Component.Model.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class DecksManager : IDecksService
    {
        private readonly List<IDeck> Decks = new List<IDeck>();

        public IDeck GetByNumber(int number)
        {
            return this.Decks.Find((Predicate<IDeck>)(each => each.Number == number));
        }

        public IDeck GetFrom(ILocation location)
        {
            return this.Decks.Find((Predicate<IDeck>)(each => each.Number == location.Deck));
        }

        public bool IsValidLocation(ILocation loc)
        {
            Deck byNumber = this.GetByNumber(loc.Deck) as Deck;
            return !(byNumber == (Deck)null) && byNumber.IsSlotValid(loc.Slot);
        }

        public void Add(IDeck deck)
        {
            if (deck.IsQlm && this.QlmDeck != null)
                throw new ArgumentException("Only one QLM deck is allowed per configuration.");
            this.Decks.Add(deck);
        }

        public void ForAllDecksDo(Predicate<IDeck> predicate)
        {
            foreach (Deck deck in this.Decks)
            {
                if (!predicate((IDeck)deck))
                    break;
            }
        }

        public void ForAllReverseDecksDo(Predicate<IDeck> predicate)
        {
            for (int count = this.Decks.Count; count >= 1; --count)
            {
                IDeck deck = this.Decks[count - 1];
                if (!predicate(deck))
                    break;
            }
        }

        public IDeck First => this.Decks[0];

        public IDeck Last => this.Decks[this.Decks.Count - 1];

        public int DeckCount => this.Decks.Count;

        public IDeck QlmDeck
        {
            get
            {
                IDeck qlmDeck = (IDeck)null;
                this.ForAllDecksDo((Predicate<IDeck>)(d =>
                {
                    if (!d.IsQlm)
                        return true;
                    qlmDeck = d;
                    return false;
                }));
                return qlmDeck;
            }
        }

        internal void Clear() => this.Decks.Clear();

        internal void Initialize(XmlNode decksNode)
        {
            this.Decks.Clear();
            foreach (XmlNode childNode in decksNode.ChildNodes)
                this.Decks.Add((IDeck)Deck.FromXmlNode(childNode));
            this.Decks.Sort((Comparison<IDeck>)((x, y) => x.Number.CompareTo(y.Number)));
            LogHelper.Instance.Log("[DecksManager] Kiosk configured for {0} decks.", (object)this.Decks.Count);
        }

        internal void SaveConfiguration(XmlDocument document)
        {
            using (StringWriter stringWriter = new StringWriter())
            {
                XmlTextWriter writer = new XmlTextWriter((TextWriter)stringWriter)
                {
                    Formatting = Formatting.Indented
                };
                foreach (IDeck deck in this.Decks)
                    (deck as Deck).ToXmlWriter((XmlWriter)writer);
                writer.Flush();
                document.DocumentElement.SelectSingleNodeAndSetInnerXml<StringWriter>("Controller/Decks", stringWriter);
            }
            LogHelper.Instance.Log("Completed decks save.", LogEntryType.Info);
        }

        internal void ToPropertyXml(XmlWriter writer)
        {
            foreach (Deck deck in this.Decks)
                deck.ToXmlWriter(writer);
        }

        internal void UpdateFromPropertyXml(XmlNode propertyNode)
        {
            XmlNodeList xmlNodeList = propertyNode.SelectNodes("Deck");
            if (xmlNodeList == null)
                return;
            List<Deck> list = new List<Deck>();
            using (new DisposeableList<Deck>((IList<Deck>)list))
            {
                if (xmlNodeList.Count != this.Decks.Count)
                {
                    LogHelper.Instance.Log("The deck configuration has changed in count; this is unsupported.");
                }
                else
                {
                    foreach (XmlNode node in xmlNodeList)
                    {
                        Deck deck = Deck.FromXmlNode(node);
                        Deck byNumber = this.GetByNumber(deck.Number) as Deck;
                        if (deck != byNumber)
                            list.Add(deck);
                    }
                    if (list.Count == 0)
                    {
                        LogHelper.Instance.Log("Update decks: there were no changes.");
                    }
                    else
                    {
                        try
                        {
                            LogHelper.Instance.Log("Update decks info:");
                            foreach (Deck newProperties in list)
                            {
                                Deck byNumber = this.GetByNumber(newProperties.Number) as Deck;
                                LogHelper.Instance.Log(" Deck Number {0} changes", (object)byNumber.Number);
                                LogHelper.Instance.Log("  Y-offset: new = {0} old = {1}", (object)newProperties.YOffset, (object)byNumber.YOffset);
                                if (newProperties.Quadrants.Count == byNumber.Quadrants.Count)
                                {
                                    for (int index = 0; index < newProperties.Quadrants.Count; ++index)
                                        LogHelper.Instance.Log("   Quadrant {0} offsets: new = {1} ( excluded = {2} ) old = {3} ( excluded = {4} )", (object)(index + 1), (object)newProperties.Quadrants[index].Offset, (object)newProperties.Quadrants[index].IsExcluded, (object)byNumber.Quadrants[index].Offset, (object)byNumber.Quadrants[index].IsExcluded);
                                    byNumber.UpdateFrom(newProperties);
                                }
                                else
                                    LogHelper.Instance.Log(LogEntryType.Error, "[DecksManager] Unsupported configuration change: slot count ( old count = {0} new count = {1}", (object)byNumber.NumberOfSlots, (object)newProperties.NumberOfSlots);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Instance.Log("There was an exception during deck update.", ex);
                        }
                    }
                }
            }
        }

        internal DecksManager()
        {
        }
    }
}
