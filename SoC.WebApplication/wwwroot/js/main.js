"use strict";

var connection = new signalR.HubConnectionBuilder().withUrl("/gameRequest").build();
var gameState = null;
var game = null;
var buttonSettlementImageName = "Button_Settlement";
var buttonRoadImageName = "Button_Road";
var buttonEndImageName = "Button_End";
var buttonBuyImageName = "Button_Buy";

connection.start().then(function () {
    var fragments = window.location.pathname.split("/");

    gameState = new Kiwi.State('State');
    gameState.gameEvents = new Queue();
    gameState.playerActions = new Queue();
    gameState.gameId = fragments[2];
    gameState.playerId = fragments[3];

    var request = {
        gameId: gameState.gameId,
        playerId: gameState.playerId
    };
    connection.invoke("ConfirmGameJoin", request).catch(function (err) {
        return console.error(err.toString());
    });
}).catch(function (err) {
    return console.error(err.toString());
});

function main() {
    gameState.preload = preloadGameState;
    gameState.create = createGameState;
    gameState.update = updateGameState;

    var gameOptions = {
        width: 800,
        height: 600
    };

    var game = new Kiwi.Game('game-container', 'soc', gameState, gameOptions);
}

connection.on("GameEvent", function (gameEvent) {
    var typeName = gameEvent.typeName;
    if (typeName === "GameJoinedEvent") {
        
    } else if (typeName === "PlayerSetupEvent") {
        var settlementColourIndexes = [2, 4, 6, 8];
        var northEastRoadColourIndexes = [2, 4, 6, 8];
        var northWestRoadColourIndexes = [11, 13, 15, 17];
        var horizontalRoadColourIndexes = [2, 4, 6, 8];
        var iconIndexes = [0, 1, 2, 3];

        var playerData = {
            players: [],
            playerById: {}
        };

        var index = 0;
        for (var playerName in gameEvent.playerIdsByName) {

            var imageIndexes = [
                settlementColourIndexes[index],
                northEastRoadColourIndexes[index],
                northWestRoadColourIndexes[index],
                horizontalRoadColourIndexes[index],
                iconIndexes[index]
            ];

            var playerId = gameEvent.playerIdsByName[playerName];
            var isLocal = (playerId === gameState.playerId);
            var player = {
                id: playerId,
                name: playerName,
                imageIndexes: imageIndexes,
                isLocal: isLocal
            }

            playerData.playerById[player.id] = player;

            index++;
        }

        gameState.playerData = playerData;

    } else if (typeName === "InitialBoardSetupEvent") {
        gameState.hexData = gameEvent.gameBoardSetup.hexData;
    } else if (typeName === "PlayerOrderEvent") {

        gameState.playerData.players = [];
        gameEvent.playerIds.forEach(function (playerId) {
            gameState.playerData.players.push(gameState.playerData.playerById[playerId]);
        });
        main();
    } else {
        gameState.gameEvents.enqueue(gameEvent);
    }
}).catch(function (err) {
    return console.error(err.toString());
});
