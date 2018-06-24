using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Controls;
using Card = Hearthstone_Deck_Tracker.Hearthstone.Card;

namespace HDT.Plugins.PlayPredictor
{
    public partial class PlayPredictorList : StackPanel
    {
        public List<Card> Cards;
        public HearthstoneTextBlock Label;
        public AnimatedCardList View;

        public PlayPredictorList()
        {
            //InitializeComponent();

            Orientation = Orientation.Vertical;

            // Section Label
            this.Label = new HearthstoneTextBlock();
            Label.FontSize = 16;
            Label.TextAlignment = TextAlignment.Center;
            Label.Text = "Opponent's Next Move";
            var margin = Label.Margin;
            margin.Top = 0;
            Label.Margin = margin;
            Children.Add(Label);
            Label.Visibility = Visibility.Hidden;

            // Card View
            this.View = new AnimatedCardList();
            Children.Add(View);
            Cards = new List<Card>();

            // TODO: How to enable card mouseover tooltips?
        }

        public void setLabel(string title)
        {
            this.Label.Text = title;
        }

        public void Update(List<Card> cards)
        {
            this.Cards = cards;
            this.View.Update(Cards, false);
            this.UpdatePosition();
            foreach(Card c in Cards)
            {
                //c.HasVisibleStats = true; READ ONLY!
                //Hearthstone_Deck_Tracker.Hearthstone.HearthDbConverter
            }
            if (cards.Count > 0) Show();
            if (cards.Count < 1) Hide();
        }

        public void UpdatePosition()
        {
            Canvas.SetTop(this, Hearthstone_Deck_Tracker.API.Core.OverlayWindow.Height * 5 / 100);
            Canvas.SetRight(this, Hearthstone_Deck_Tracker.API.Core.OverlayWindow.Width * 20 / 100);
        }

        public void Show()
        {
            this.Label.Visibility = Visibility.Visible;
            this.View.Visibility = Visibility.Visible;
            this.Visibility = Visibility.Visible;
        }

        public void Hide()
        {
            this.Label.Visibility = Visibility.Hidden;
            this.View.Visibility = Visibility.Hidden;
            this.Visibility = Visibility.Hidden;
        }
    }
}