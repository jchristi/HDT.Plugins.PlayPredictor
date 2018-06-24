using System.Collections.Generic;
using System.Linq;

using RestSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using HearthDb.Enums;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;
using Hearthstone_Deck_Tracker.Utility.Logging;
using CoreAPI = Hearthstone_Deck_Tracker.API.Core;
using System;

namespace HDT.Plugins.PlayPredictor
{
    //
    // Most of the information you need you will be able to get from the Hearthstone.Game class
    //
    // Hearthstone.Game.Entities has information about everything in a game, from card locations (hand, board) 
    // to health and what card a card was last affected by. You may want to have a look at Enums.Hearthstone.GAME_TAG for this.
    //
    // The Debug Window: The debug window be opened via options > tracker > settings.
    //  * This window displays the properties of Game and contents of Game.Entities live.
    //  * The Advanced Options Checkbox (located in the lower Left corner) must be checked in-order to see the logging options.
    //
    // Log Files: Clicking "Open AppData Folder", also located under options > tracker > settings, will open an explorer window 
    // to the directory containing the HDT config.xml file and default logging directory.
    //
    // To help save some time in the development process, you can create a Post-Build event to copy the plug-in to your HDT plug-in folder like:
    //   * copy "$(TargetDir)$(ProjectName).*" "YourTrackerPathGoesHere\Plugins"
    // Remember to remove this command if/before you check into Git.
    //
    
    internal class PlayPredictor
    {
        // keep track of mana count
        private int _mana = 0;

        // max results to display
        private int _maxResults = 10;

        // cache of card popularity data fetched from hsreplays.net
        private List<PopularityCard> _popularCards = null;

        // overlay of card list
        private PlayPredictorList _playPredictorList = null;

        // another list?
        internal List<Entity> Entities =>
             Helper.DeepClone<Dictionary<int, Entity>>(CoreAPI.Game.Entities).Values.ToList<Entity>();
        // opponent object
        internal Entity Opponent => Entities?.FirstOrDefault(x => x.IsOpponent);

        // Constructor
        public PlayPredictor(PlayPredictorList playPredictorList)
        {
            _playPredictorList = playPredictorList;
            _playPredictorList.Hide();
            // Hide in menu, if necessary
            if (Config.Instance.HideInMenu && CoreAPI.Game.IsInMenu)
            {
                _playPredictorList.Hide();
            }

            // TODO: Get Config for Max Results
            //this.maxResults = Config.Instance.
        }

        /**
         * Initialize events
         */
        public void Init()
        {
            // Triggered upon startup and when the user ticks the plugin on
            GameEvents.OnGameStart.Add(this.GameStart);
            GameEvents.OnTurnStart.Add(this.TurnStart);
            GameEvents.OnInMenu.Add(this.Hide);
            GameEvents.OnGameEnd.Add(this.GameEnd);

            // Search Hearthstone_Deck_Tracker.API.GameEvents.* for all sorts of draw, play, game start/end events
            // Search Hearthstone_Deck_Tracker.API.DeckManagerEvents.* for events related to deck creation, deletion, etc.
        }

        internal class JsonCard
        {
            public int dbf_id;
            public double popularity;
            public double? winrate;
            public int total; // when: query == "card_played_popularity_report" && gameType == "RANKED_STANDARD"
            public int decks; // when: query == "card_played_popularity_report" && gameType == "ARENA"
        }

        internal class PopularityCard
        {
            public JsonCard jsonCard;
            public HearthDb.Card dbCard;

            public PopularityCard(HearthDb.Card dbCard, JsonCard j)
            {
                this.dbCard = dbCard;
                this.jsonCard = j;
            }
        }

        internal List<PopularityCard> GetPopularCards()
        {
            // return cached results immediately
            if (_popularCards != null) return _popularCards;

            // example URL from hsreplay.net:
            // https://hsreplay.net/analytics/query/card_played_popularity_report/?GameType=RANKED_STANDARD&TimeRange=CURRENT_PATCH&RankRange=ALL
            var baseUrl = "https://hsreplay.net"; ;
            var client = new RestClient(baseUrl);
            var gameType = "ARENA";
            var timeRange = "ARENA_EVENT";
            var rankRange = "ALL";
            if (CoreAPI.Game.CurrentGameType == GameType.GT_ARENA)
            {
                gameType = "ARENA";
                timeRange = "ARENA_EVENT";
            }
            else
            {
                gameType = CoreAPI.Game.CurrentFormat == Format.Wild && CoreAPI.Game.CurrentGameType != GameType.GT_VS_AI ? "RANKED_WILD" : "RANKED_STANDARD";
                timeRange = "CURRENT_PATCH";
            }
            Log.Info($"CoreAPI.Game.CurrentGameType={CoreAPI.Game.CurrentGameType}");
            Log.Info($"CoreAPI.Game.CurrentFormat={CoreAPI.Game.CurrentFormat}");
            Log.Info($"gameType: {gameType}");
            Log.Info($"timeRange: {timeRange}");
            var request = new RestRequest("analytics/query/{query}/?GameType={gameType}&TimeRange={timeRange}&RankRange={rankRange}", Method.GET);
            request.AddUrlSegment("query", "card_played_popularity_report");
            request.AddUrlSegment("gameType", gameType);
            request.AddUrlSegment("timeRange", timeRange);
            request.AddUrlSegment("rankRange", rankRange);
            IRestResponse response = client.Execute(request);
            Log.Info(response.ResponseUri.ToString());
            JObject json = JObject.Parse(response.Content);
            var opponentHeroClass = CoreAPI.Game.Opponent.Class.ToUpper();
            // TODO: check if opponentHeroClass in series.data
            JArray heroData = (JArray)json["series"]["data"][opponentHeroClass];
            List<JsonCard> jl = (List<JsonCard>)heroData.ToObject(typeof(List<JsonCard>));
            List<PopularityCard> popularCards = new List<PopularityCard>();
            foreach (JsonCard j in jl)
            {
                PopularityCard pc = HearthDb.Cards.Collectible.Values
                    .Where(c => c.DbfId == j.dbf_id)
                    .Select(c => new PopularityCard(c, j))
                    .FirstOrDefault();
                if (pc == null) continue;
                popularCards.Add(pc);
            }
            _popularCards = popularCards;
            return popularCards;

            // https://hsreplay.net/cards/290/ironbeak-owl/#tab=turn-statistics&opponentClass=DRUID
            // curl 'https://hsreplay.net/analytics/query/single_card_stats_by_turn_and_opponent/?GameType=RANKED_STANDARD&card_id=1124&RankRange=ALL' 
            // -H 'accept-encoding: gzip, deflate, br' 
            // -H 'accept-language: en-US,en;q=0.9' 
            // -H 'accept: */*' 
            // -H 'referer: https://hsreplay.net/cards/1124/wild-growth/'
            // -H 'authority: hsreplay.net' 
            // -H 'cookie: 
            //      __cfduid=db4e7db037fa2f6eb43e7d4cb9e3060d41509728025; 
            //      knows-about-archetypes=1; 
            //      default-account=1-115864767; 
            //      __stripe_mid=b6a7932b-94aa-432d-95a7-f0b06d736605; 
            //      csrftoken=9EmWOSbYbGiq61PaCPiRGqOrpme65XpnGuxEQr1Fz1r7Ek8MA3JXJxpG3hK7Nunq; 
            //      sessionid=ovuhgdi5q1l86ewehmy2yfomed2hi1hw'

            // oath
            // https://hsreplay.net/account/battlenet/login/?next=%2Fdecks%2FrpxDe8CfeNjWsZ8dqJoikf%2F&region=us
            // returns 302 -> https://us.battle.net/oauth/authorize?client_id=5uga73b9p2bevf3p78wdjkfwfftqvswg&redirect_uri=https%3A%2F%2Fhsreplay.net%2Faccount%2Fbattlenet%2Flogin%2Fcallback%2F%3Fregion%3Dus&scope=&response_type=code&state=X7m60QRLzisU
            // https://us.battle.net/oauth/authorize?client_id=5uga73b9p2bevf3p78wdjkfwfftqvswg&redirect_uri=https%3A%2F%2Fhsreplay.net%2Faccount%2Fbattlenet%2Flogin%2Fcallback%2F%3Fregion%3Dus&scope=&response_type=code&state=X7m60QRLzisU
            // returns 302 -> https://us.battle.net/login/en/?ref=https://us.battle.net/oauth/authorize?client_id%3D5uga73b9p2bevf3p78wdjkfwfftqvswg%26redirect_uri%3Dhttps%253A%252F%252Fhsreplay.net%252Faccount%252Fbattlenet%252Flogin%252Fcallback%252F%253Fregion%253Dus%26scope%26response_type%3Dcode%26state%3DX7m60QRLzisU&app=oauth&opt
            //      client_id: 5uga73b9p2bevf3p78wdjkfwfftqvswg
            //      redirect_uri: https://hsreplay.net/account/battlenet/login/callback/?region=us
            // https://us.battle.net/login/en/?ref=https://us.battle.net/oauth/authorize?client_id%3D5uga73b9p2bevf3p78wdjkfwfftqvswg%26redirect_uri%3Dhttps%253A%252F%252Fhsreplay.net%252Faccount%252Fbattlenet%252Flogin%252Fcallback%252F%253Fregion%253Dus%26scope%26response_type%3Dcode%26state%3DX7m60QRLzisU&app=oauth&opt
            // https://us.battle.net/oauth/authorize?client_id=5uga73b9p2bevf3p78wdjkfwfftqvswg&redirect_uri=https%3A%2F%2Fhsreplay.net%2Faccount%2Fbattlenet%2Flogin%2Fcallback%2F%3Fregion%3Dus&scope&response_type=code&state=X7m60QRLzisU&ST=US-670109be44dcdc68e822c0bb22183560-63938717
            //  -> sets a bunch of cookies
            // https://us.battle.net/oauth/authorize?client_id=5uga73b9p2bevf3p78wdjkfwfftqvswg&redirect_uri=https%3A%2F%2Fhsreplay.net%2Faccount%2Fbattlenet%2Flogin%2Fcallback%2F%3Fregion%3Dus&scope&response_type=code&state=X7m60QRLzisU
            // https://hsreplay.net/account/battlenet/login/callback/?region=us&code=vbvsjqafqnvybp7mtjph4h3n&state=X7m60QRLzisU
            // https://hsreplay.net/decks/rpxDe8CfeNjWsZ8dqJoikf/
        }

        // Calculate the mana opponent will have on his next turn
        internal int AvailableMana()
        {
            if (this.Opponent != null)
            {
                var mana = this.Opponent.GetTag(GameTag.RESOURCES);
                Log.Info($"Opponent Mana: {mana}");
                var coin = CoreAPI.Game.Opponent.HasCoin ? 1 : 0;
                Log.Info($"Opponent Has Coin?: {coin}");
                var overload = this.Opponent.GetTag(GameTag.OVERLOAD_OWED);
                Log.Info($"Opponent Overload: {overload}");
                _mana = Math.Min(mana - overload + coin + 1, 10);
            }
            return _mana;
        }

        internal void GameStart()
        {
            _mana = 0;
            _playPredictorList.Update(new List<Card>());
            _playPredictorList.Hide();
        }

        internal void TurnStart(ActivePlayer player)
        {
            // TODO: return null if no game in progress

            if (CoreAPI.Game.CurrentGameType > GameType.GT_CASUAL)
            {
                // Game type not supported
                return;
            }

            if (this.Opponent == null)
            {
                // error
            }

            if (player != ActivePlayer.Player)
                this.OpponentTurnStart();
            else
                this.PlayerTurnStart();
        }

        internal void PlayerTurnStart()
        {
            var mana = this.AvailableMana();
            var klass = this.KlassConverter(CoreAPI.Game.Opponent.Class);
            var isWild = CoreAPI.Game.CurrentFormat == Format.Wild && CoreAPI.Game.CurrentGameType != GameType.GT_VS_AI;
            Log.Info($"isWild?: {isWild}");
            List<Card> predictedCards = GetPopularCards() // TODO: Error handling
                .Where(c => (c.dbCard.Cost >= mana - 1 && c.dbCard.Cost <= mana) // TODO: include sea giant, mountain giant, etc
                    && (c.dbCard.Class == klass || c.dbCard.Class == CardClass.NEUTRAL)
                    && c.dbCard.IsWild == isWild)
                .OrderByDescending(c => c.jsonCard.popularity)
                .Take(_maxResults)
                .Select(c => new Card(c.dbCard))
                .ToList();
            foreach (Card c in predictedCards)
            {
                Log.Info($"cardName={c.Name}, Dbfif={c.DbfIf}, id={c.Id}, cost={c.Cost}, class={c.GetPlayerClass}, set={c.Set}");
            }
            _playPredictorList.setLabel($"Opponent's Next Move ({mana})");
            _playPredictorList.Update(predictedCards);
        }

        internal void OpponentTurnStart()
        {
            //_playPredictorList.Hide();
        }

        // Delete old cached data and hide the view
        internal void GameEnd()
        {
            this.Hide();
            _popularCards = null;
        }

        // hide the view
        internal void Hide()
        {
            _playPredictorList.Hide();
        }

        // Convert hero class string to enum
        internal CardClass KlassConverter(string klass)
        {
            switch (klass.ToLowerInvariant())
            {
                case "druid":
                    return CardClass.DRUID;
                case "hunter":
                    return CardClass.HUNTER;
                case "mage":
                    return CardClass.MAGE;
                case "paladin":
                    return CardClass.PALADIN;
                case "priest":
                    return CardClass.PRIEST;
                case "rogue":
                    return CardClass.ROGUE;
                case "shaman":
                    return CardClass.SHAMAN;
                case "warlock":
                    return CardClass.WARLOCK;
                case "warrior":
                    return CardClass.WARRIOR;
                default:
                    return CardClass.NEUTRAL;
            }
        }
    }
}