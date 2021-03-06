﻿
namespace SoC.Library.ScenarioTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Jabberwocky.SoC.Library;
    using Jabberwocky.SoC.Library.GameEvents;
    using Jabberwocky.SoC.Library.Interfaces;
    using Newtonsoft.Json.Linq;
    using SoC.Library.ScenarioTests.Instructions;
    using SoC.Library.ScenarioTests.ScenarioEvents;

    internal class ScenarioPlayerAgent
    {
        #region Fields
        private readonly List<GameEvent> actualEvents = new List<GameEvent>();
        private readonly ConcurrentQueue<GameEvent> actualEventQueue = new ConcurrentQueue<GameEvent>();
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly List<JToken> didNotReceiveEvents = new List<JToken>();
        private readonly Dictionary<Type, int> maximumEventTypeCountsByEventType = new Dictionary<Type, int>();
        private readonly Dictionary<Type, int> eventTypeCountsByEventType = new Dictionary<Type, int>();
        private readonly List<EventActionPair> expectedEventActions = new List<EventActionPair>();
        private readonly HashSet<GameEvent> expectedEventsWithVerboseLogging = new HashSet<GameEvent>();
        private readonly IPlayerAgentLog log = new ScenarioPlayerAgentLog();
        private readonly bool verboseLogging;
        private CancellationToken cancellationToken;
        private int expectedEventIndex;
        private GameController gameController;
        private IDictionary<string, Guid> playerIdsByName;
        #endregion

        #region Construction
        public ScenarioPlayerAgent(string name, bool verboseLogging = false)
        {
            this.Name = name;
            this.verboseLogging = verboseLogging;
            this.Id = Guid.NewGuid();
            this.cancellationToken = this.cancellationTokenSource.Token;
        }
        #endregion

        #region Properties
        public bool FinishWhenAllEventsVerified { get; set; } = true;
        public GameController GameController
        {
            private get { return this.gameController; }
            set
            {
                this.gameController = value;
                this.gameController.GameEvent += this.GameEventHandler;
            }
        }
        public Guid Id { get; private set; }
        public bool IsFinished { get { return this.expectedEventIndex >= this.expectedEventActions.Count; } }
        public string Name { get; private set; }
        public ResourceClutch Resources { get { return this.GameController.Resources; } }
        private EventActionPair CurrentEventActionPair { get { return this.expectedEventActions[this.expectedEventIndex]; } }
        private EventActionPair LastEventActionPair { get { return this.expectedEventActions[this.expectedEventActions.Count - 1]; } }
        #endregion

        #region Methods
        public void AddDidNotReceiveEvent(GameEvent gameEvent)
        {
            var gameEventToken = JToken.Parse(gameEvent.ToJSONString());
            this.didNotReceiveEvents.Add(gameEventToken);
        }

        public void AddDidNotReceiveEventOfType<T>() where T : GameEvent
        {
            this.AddDidNotReceiveEventOfType<T>(0);
        }

        public void AddDidNotReceiveEventOfType<T>(int eventCount) where T : GameEvent
        {
            this.maximumEventTypeCountsByEventType.Add(typeof(T), eventCount);
            this.eventTypeCountsByEventType.Add(typeof(T), 0);
        }

        public void AddInstruction(Instruction instruction)
        {
            if (instruction is EventInstruction eventInstruction)
            {
                this.expectedEventActions.Add(new EventActionPair(eventInstruction.GetEvent()));
            }
            else if (instruction is ActionInstruction actionInstruction)
            {
                if (this.LastEventActionPair.Action != null)
                    throw new Exception($"Last event action already set with operation {this.LastEventActionPair.Action.Operation}");
                this.LastEventActionPair.Action = actionInstruction;
            }
            else if (instruction is PlayerStateInstruction playerStateInstruction)
            {
                if (this.LastEventActionPair.Action != null)
                    throw new Exception($"Last event action already set with operation {this.LastEventActionPair.Action.Operation}");
                this.LastEventActionPair.Action = playerStateInstruction.GetAction();
                this.expectedEventActions.Add(new EventActionPair(playerStateInstruction.GetEvent()));
            }
            else if (instruction is MultipleEventInstruction multipleEventInstruction)
            {
                var eventActionPair = new EventActionPair();
                foreach (var gameEvent in multipleEventInstruction.Events)
                    eventActionPair.Add(gameEvent);
                this.expectedEventActions.Add(eventActionPair);
            }
        }

        public string GetEventLog()
        {
            string contents = null;
            int number = 1;
            this.expectedEventActions.ForEach(eventAction => {
                contents += $"{number++:00} {eventAction.ToString()}\r\n";
            });
            return contents;
        }

        public void Quit()
        {
            this.cancellationTokenSource.Cancel();
        }

        public void SaveEvents(string filePath)
        {
            System.IO.File.WriteAllText(filePath, this.GetEventLog());
        }

        public void SaveLog(string filePath) => this.log.WriteToFile(filePath);

        public Task StartAsync()
        {
            return Task.Factory.StartNew(o => { this.Run(); }, this, this.cancellationToken);
        }

        private void GameEventHandler(GameEvent gameEvent)
        {
            this.actualEventQueue.Enqueue(gameEvent);
        }

        private void Run()
        {
            Thread.CurrentThread.Name = this.Name;

            try
            {
                while (!this.IsFinished)
                {
                    this.WaitForGameEvent();
                    this.VerifyEvents();
                }

                if (!this.FinishWhenAllEventsVerified)
                {
                    this.log.AddNote("Continue running and receiving game events");
                    while (!this.cancellationToken.IsCancellationRequested)
                    {
                        Thread.Sleep(50);
                        if (this.actualEventQueue.TryDequeue(out var actualEvent))
                        {
                            this.VerifyActualEvent(actualEvent);
                            this.log.AddActualEvent(actualEvent);
                        }
                    }
                }

                this.log.AddNote("Finished");
            }
            catch (OperationCanceledException)
            {
                try
                {
                    for (var i = this.expectedEventIndex; i < this.expectedEventActions.Count; i++)
                    {
                        var expectedEventAction = this.expectedEventActions[i];
                        for (var j = 0; j < expectedEventAction.Statuses.Count; j++)
                        {
                            if (!expectedEventAction.Statuses[j])
                                this.log.AddUnmatchedExpectedEvent(expectedEventAction.ExpectedEvents[j]);
                        }
                    }
                }
                catch
                {
                    // Ignore exceptions happening here
                }

                return;
            }
            catch (ReceivedUnwantedEventException r)
            {
                this.log.AddReceivedUnwantedEventException(r);
                throw;
            }
            catch (Exception e)
            {
                this.log.AddException(e);
                throw;
            }
        }

        private void SendAction(ActionInstruction action)
        {
            this.log.AddAction(action);
            switch (action.Operation)
            {
                case ActionInstruction.OperationTypes.AcceptTrade:
                {
                    this.GameController.AcceptDirectTradeOffer(this.playerIdsByName[(string)action.Parameters[0]]);
                    break;
                }
                case ActionInstruction.OperationTypes.AnswerDirectTradeOffer:
                {
                    this.GameController.AnswerDirectTradeOffer((ResourceClutch)action.Parameters[0]);
                    break;
                }
                case ActionInstruction.OperationTypes.BuyDevelopmentCard:
                {
                    this.GameController.BuyDevelopmentCard();
                    break;
                }
                case ActionInstruction.OperationTypes.ChooseResourcesToLose:
                {
                    this.GameController.ChooseResourcesToLose((ResourceClutch)action.Parameters[0]);
                    break;
                }
                case ActionInstruction.OperationTypes.ConfirmStart:
                {
                    this.GameController.ConfirmStart();
                    break;
                }
                case ActionInstruction.OperationTypes.EndOfTurn:
                {
                    this.GameController.EndTurn();
                    break;
                }
                case ActionInstruction.OperationTypes.MakeDirectTradeOffer:
                {
                    this.GameController.MakeDirectTradeOffer((ResourceClutch)action.Parameters[0]);
                    break;
                }
                case ActionInstruction.OperationTypes.PlaceCity:
                {
                    this.GameController.PlaceCity((uint)action.Parameters[0]);
                    break;
                }
                case ActionInstruction.OperationTypes.PlaceRoadSegment:
                {
                    this.GameController.PlaceRoadSegment((uint)action.Parameters[0], (uint)action.Parameters[1]);
                    break;
                }
                case ActionInstruction.OperationTypes.PlaceRobber:
                {
                    this.GameController.PlaceRobber((uint)action.Parameters[0]);
                    break;
                }
                case ActionInstruction.OperationTypes.PlaceSettlement:
                {
                    this.GameController.PlaceSettlement((uint)action.Parameters[0]);
                    break;
                }
                case ActionInstruction.OperationTypes.PlaceStartingInfrastructure:
                {
                    this.GameController.PlaceSetupInfrastructure((uint)action.Parameters[0], (uint)action.Parameters[1]);
                    break;
                }
                case ActionInstruction.OperationTypes.PlayKnightCard:
                {
                    this.GameController.PlayKnightCard((uint)action.Parameters[0]);
                    break;
                }
                case ActionInstruction.OperationTypes.PlayMonopolyCard:
                {
                    this.GameController.PlayMonopolyCard((ResourceTypes)action.Parameters[0]);
                    break;
                }
                case ActionInstruction.OperationTypes.PlayRoadBuildingCard:
                {
                    this.GameController.PlayRoadBuildingCard(
                        (uint)action.Parameters[0],
                        (uint)action.Parameters[1],
                        (uint)action.Parameters[2],
                        (uint)action.Parameters[3]);
                    break;
                }
                case ActionInstruction.OperationTypes.PlayYearOfPlentyCard:
                {
                    this.GameController.PlayYearOfPlentyCard((ResourceTypes)action.Parameters[0], (ResourceTypes)action.Parameters[1]);
                    break;
                }
                case ActionInstruction.OperationTypes.RequestState:
                {
                    this.GameController.RequestState();
                    break;
                }
                case ActionInstruction.OperationTypes.QuitGame:
                {
                    this.GameController.QuitGame();
                    break;
                }
                case ActionInstruction.OperationTypes.SelectResourceFromPlayer:
                {
                    this.GameController.SelectResourceFromPlayer(this.playerIdsByName[(string)action.Parameters[0]]);
                    break;
                }
                default: throw new Exception($"Operation '{action.Operation}' not recognised");
            }
        }

        private bool IsEventVerified(GameEvent expectedEvent, GameEvent actualEvent)
        {
            if (expectedEvent is ScenarioRequestStateEvent expectedRequestEvent && actualEvent is RequestStateEvent actualRequestEvent)
                return this.IsRequestStateEventVerified(expectedRequestEvent, actualRequestEvent);
            else if (expectedEvent is ScenarioGameErrorEvent expectedErrorEvent && actualEvent is GameErrorEvent actualErrorEvent)
                return this.IsGameErrorEventVerified(expectedErrorEvent, actualErrorEvent);
            else if (expectedEvent is ScenarioStartTurnEvent expectedStartEvent && actualEvent is StartTurnEvent actualStartEvent)
                return this.IsStartTurnEventVerified(expectedStartEvent, actualStartEvent);
            
            return this.IsStandardEventVerified(expectedEvent, actualEvent);
        }

        private bool IsGameErrorEventVerified(ScenarioGameErrorEvent expectedEvent, GameErrorEvent actualEvent)
        {
            var result = true;
            if (expectedEvent.Id.HasValue)
                result &= expectedEvent.Id.Value == actualEvent.PlayerId;
            //if (expectedEvent.ErrorCode != null)
                result &= expectedEvent.ErrorCode == actualEvent.ErrorCode;
            if (expectedEvent.ErrorMessage != null)
                result &= expectedEvent.ErrorMessage == actualEvent.ErrorMessage;

            if (result)
                this.log.AddMatchedEvent(actualEvent, expectedEvent);

            return result;
        }

        private bool IsRequestStateEventVerified(ScenarioRequestStateEvent expectedEvent, RequestStateEvent actualEvent)
        {
            var result = true;
            if (expectedEvent.Cities.HasValue)
                result &= expectedEvent.Cities.Value == actualEvent.Cities;
            if (expectedEvent.DevelopmentCardsByCount != null)
                result &= this.IsDictionaryMatched(expectedEvent.DevelopmentCardsByCount, actualEvent.DevelopmentCardsByCount);
            if (expectedEvent.HeldCards != null)
                result &= expectedEvent.HeldCards.Value == actualEvent.HeldCards;
            if (expectedEvent.PlayedKnightCards.HasValue)
                result &= expectedEvent.PlayedKnightCards.Value == actualEvent.PlayedKnightCards;
            if (expectedEvent.Resources.HasValue)
                result &= expectedEvent.Resources.Value == actualEvent.Resources;
            if (expectedEvent.RoadSegments.HasValue)
                result &= expectedEvent.RoadSegments.Value == actualEvent.RoadSegments;
            if (expectedEvent.Settlements.HasValue)
                result &= expectedEvent.Settlements.Value == actualEvent.Settlements;
            if (expectedEvent.VictoryPoints.HasValue)
                result &= expectedEvent.VictoryPoints.Value == actualEvent.VictoryPoints;

            return result;
        }

        private bool IsStandardEventVerified(GameEvent expectedEvent, GameEvent actualEvent)
        {
            var expectedJSON = JToken.Parse(expectedEvent.ToJSONString());
            var actualJSON = JToken.Parse(actualEvent.ToJSONString());
            var result = JToken.DeepEquals(expectedJSON, actualJSON);

            return result;
        }

        private bool IsDictionaryMatched(Dictionary<DevelopmentCardTypes, int> first, Dictionary<DevelopmentCardTypes, int> second)
        {
            var secondKeys = new List<DevelopmentCardTypes>(second.Keys);
            if (secondKeys.Count == 0)
            {
                foreach (var kv in first)
                {
                    if (kv.Value > 0)
                        return false;
                }

                return true;
            }

            var firstKeys = new List<DevelopmentCardTypes>(first.Keys);
            if (firstKeys.Count != secondKeys.Count)
                return false;

            firstKeys.Sort();
            secondKeys.Sort();

            for (var i = 0; i < firstKeys.Count; i++)
            {
                if (firstKeys[i] != secondKeys[i])
                    return false;

                if (first[firstKeys[i]] != second[secondKeys[i]])
                    return false;
            }

            return true;
        }

        private bool IsStartTurnEventVerified(ScenarioStartTurnEvent expectedEvent, StartTurnEvent actualEvent)
        {
            var result = expectedEvent.PlayerId == actualEvent.PlayerId &&
                expectedEvent.Dice1 == actualEvent.Dice1 &&
                expectedEvent.Dice2 == actualEvent.Dice2;

            return result;
        }

        public void SetVerboseLoggingOnVerificationOfPreviousEvent(bool verboseLogging)
        {
            throw new NotImplementedException();
        }

        private string GetPlayerName(Guid playerId)
        {
            foreach(var kv in this.playerIdsByName)
            {
                if (kv.Value == playerId)
                    return kv.Key;
            }

            throw new KeyNotFoundException();
        }

        private void VerifyEvents()
        {
            if (this.expectedEventIndex >= this.expectedEventActions.Count)
                return;

            GameEvent actualEvent = this.actualEvents[this.actualEvents.Count - 1];
            var matched = false;
            for (var i = 0; i < this.CurrentEventActionPair.ExpectedEvents.Count; i++)
            {
                if (this.CurrentEventActionPair.Statuses[i])
                    continue;

                if (this.IsEventVerified(this.CurrentEventActionPair.ExpectedEvents[i], actualEvent))
                { 
                    this.CurrentEventActionPair.Statuses[i] = true;
                    this.log.AddMatchedEvent(actualEvent, this.CurrentEventActionPair.ExpectedEvents[i]);
                    matched = true;
                    break;
                }
            }

            if (!matched)
                this.log.AddActualEvent(actualEvent);

            var finished = this.CurrentEventActionPair.Statuses.All(status => status);
            if (finished)
            {
                if (this.CurrentEventActionPair.Action != null)
                    this.SendAction(this.CurrentEventActionPair.Action);
                this.expectedEventIndex++;
            }
        }

        private void WaitForGameEvent()
        {
            while (true)
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(50);
                if (!this.actualEventQueue.TryDequeue(out var actualEvent))
                    continue;

                if (actualEvent is PlayerSetupEvent playerSetupEvent)
                {
                    this.playerIdsByName = playerSetupEvent.PlayerIdsByName;
                    ScenarioPlayerAgentLog.PlayerNamesById = this.playerIdsByName.ToDictionary(kv => kv.Value, kv => kv.Key);
                }

                this.VerifyActualEvent(actualEvent);
                
                this.actualEvents.Add(actualEvent);
                break;
            }
        }

        private void VerifyActualEvent(GameEvent actualEvent)
        {
            var actualEventType = actualEvent.GetType();
            if (this.maximumEventTypeCountsByEventType.ContainsKey(actualEventType))
            {
                this.eventTypeCountsByEventType[actualEventType]++;
                if (this.eventTypeCountsByEventType[actualEventType] > 
                    this.maximumEventTypeCountsByEventType[actualEventType])
                {
                    if (this.maximumEventTypeCountsByEventType[actualEventType] > 0)
                    {
                        throw new ReceivedUnwantedEventException($"Received {this.eventTypeCountsByEventType[actualEventType]} event(s)" +
                            $" of type {actualEvent.SimpleTypeName} but should have received only " +
                            $"{this.maximumEventTypeCountsByEventType[actualEventType]}", actualEvent);
                    }

                    throw new ReceivedUnwantedEventException($"Received event of type {actualEvent.SimpleTypeName} but should not have", actualEvent);
                }
            }

            if (this.didNotReceiveEvents.FirstOrDefault(d => JToken.DeepEquals(d, JToken.Parse(actualEvent.ToJSONString()))) != null)
                throw new ReceivedUnwantedEventException($"Received event {actualEvent.SimpleTypeName} but should not have", actualEvent);
        }
        #endregion

        #region Structures
        private class EventActionPair
        {
            public EventActionPair() { }
            public EventActionPair(GameEvent gameEvent)
            {
                this.Add(gameEvent);
            }

            public List<GameEvent> ExpectedEvents { get; private set; } = new List<GameEvent>();
            public ActionInstruction Action { get; set; }
            public List<bool> Statuses { get; set; } = new List<bool>();

            public void Add(GameEvent gameEvent)
            {
                this.ExpectedEvents.Add(gameEvent);
                this.Statuses.Add(false);
            }

            public override string ToString()
            {
                return $"{this.ToExpectedEventString()} -> " +
                    $"{(this.Action != null ? "ACTION: " + this.Action.Operation : "(no action)")}";
            }

            private string ToExpectedEventString()
            {
                string result = null;
                for (var i = 0; i < this.ExpectedEvents.Count; i++)
                {
                    var expectedEvent = this.ExpectedEvents[i];
                    if (expectedEvent is ScenarioStartTurnEvent scenarioStartTurnEvent)
                        result += $"{scenarioStartTurnEvent.SimpleTypeName}[{scenarioStartTurnEvent.Dice1},{scenarioStartTurnEvent.Dice2}]";
                    else
                        result += expectedEvent.SimpleTypeName;

                    result += this.Statuses[i] == false ? " (NOT VERIFIED), " : ", ";
                }

                return result.Substring(0, result.Length - 2);
            }
        }
        #endregion
    }
}
