﻿"use strict"

function displayBoard(state, layoutColumnData, hexData, textures) {
    var hexindex = 0;

    for (var index = 0; index < layoutColumnData.data.length; index++) {
        var columnData = layoutColumnData.data[index];
        var y = columnData.y;
        var count = columnData.count;
        while (count-- > 0) {
            var hex = hexData[hexindex++];
            var hexImage = new Kiwi.GameObjects.StaticImage(state, textures.hextypes, columnData.x, y);
            hexImage.cellIndex = hex.resourceType != null ? hex.resourceType : 5;
            state.addChild(hexImage);
            if (hex.productionFactor != 0) {
                var productionImage = new Kiwi.GameObjects.StaticImage(state, textures.productionfactors, columnData.x, y);
                productionImage.cellIndex = hex.productionFactor;
                state.addChild(productionImage);
            }
            y += layoutColumnData.deltaY;
        }
    }
}

function setupInitialPlacementUI(state, textures, settlementPlacementData, roadPlacementData, imageIndexesById) {
    var initialPlacementManager = new InitialPlacementManager(state, textures, imageIndexesById,
        function (context, params) { initialPlacementManager.onConfirm(); },
        function (context, params) { initialPlacementManager.onCancelSettlement(); },
        function (context, params) { initialPlacementManager.onCancelRoad(); });

    var sprites = [];

    var settlementClickedHandler = function (context, params) {
        initialPlacementManager.handleSettlementClick(context.id);
    };

    var settlementHoverEnterHandler = function (context, params) {
        initialPlacementManager.handleSettlementEnter(context.id);
    }

    var settlementHoverLeftHandler = function (context, params) {
        initialPlacementManager.handleSettlementLeft(context.id);
    }

    var halfSettlementIconWidth = 12;
    var halfSettlementIconHeight = 12;
    var settlementLocation = 0;
    for (var index = 0; index < settlementPlacementData.length; index++) {
        var columnPlacementData = settlementPlacementData[index];
        var x = columnPlacementData.x - halfSettlementIconWidth;
        var y = columnPlacementData.y - halfSettlementIconHeight;
        var offsets = columnPlacementData.offsets;
        var offsetCount = offsets.length + 1;
        var offsetIndex = 0;
        while (offsetCount > 0) {
            var settlementSprite = new Kiwi.GameObjects.Sprite(state, textures.settlement, x, y);
            settlementSprite.visible = false;
            settlementSprite.input.onUp.add(settlementClickedHandler, state);
            settlementSprite.input.onEntered.add(settlementHoverEnterHandler, state);
            settlementSprite.input.onLeft.add(settlementHoverLeftHandler, state);

            initialPlacementManager.addSettlementSprite(settlementSprite, settlementLocation++);
            sprites.push(settlementSprite);

            if (offsetCount > 1) {
                x += offsets[offsetIndex].deltaX;
                y += offsets[offsetIndex++].deltaY;
            }

            offsetCount--;
        }
    }

    var roadClickedHandler = function (context, params) {
        initialPlacementManager.handleRoadClick(context.id);
    }

    var roadHoverEnterHandler = function (context, params) {
        initialPlacementManager.handleRoadHoverEnter(context.id);
    }

    var roadHoverLeftHandler = function (context, params) {
        initialPlacementManager.handleRoadHoverLeft(context.id);
    }

    for (var roadCollectionData of roadPlacementData) {
        for (var roadData of roadCollectionData.roads) {
            var roadSprite = new Kiwi.GameObjects.Sprite(state, textures[roadCollectionData.imageName], roadData.x, roadData.y);
            roadSprite.cellIndex = roadCollectionData.imageIndex;
            roadSprite.visible = false;
            roadSprite.input.onUp.add(roadClickedHandler, state);
            roadSprite.input.onEntered.add(roadHoverEnterHandler, state);
            roadSprite.input.onLeft.add(roadHoverLeftHandler, state);

            var locations = roadData.locations;
            initialPlacementManager.addRoadPlacement(roadSprite, roadCollectionData.imageIndex, roadCollectionData.hoverImageIndex,
                roadCollectionData.type, sprites[locations[0]].id, locations[0], sprites[locations[1]].id, locations[1]);
            sprites.push(roadSprite);
        }
    }

    for (var i = sprites.length - 1; i >= 0; i--)
        state.addChild(sprites[i]);

    return initialPlacementManager;
}

function createGameState() {
    Kiwi.State.prototype.create(this);
    this.background = new Kiwi.GameObjects.StaticImage(this, this.textures.background, 0, 0);
    var backgroundWidth = this.background.width;
    var backgroundHeight = this.background.height;
    this.addChild(this.background);

    var originX = (backgroundWidth / 2);
    var originY = (backgroundHeight / 2);
    displayBoard(this, getTilePlacementData(originX, originY), hexData, this.textures);

    this.initialPlacementManager = setupInitialPlacementUI(this, this.textures,
        getSettlementPlacementData(originX, originY), getRoadPlacementData(originX, originY),
        this.imageIndexesById);

    this.currentPlayerMarker = new Kiwi.GameObjects.Sprite(this, this.textures.playermarker, 90, 5);
    this.currentPlayerMarker.visible = false;
    this.currentPlayerMarker.animation.add('main', [2, 1, 0], 0.15, true, false);
    this.addChild(this.currentPlayerMarker);

    this.players = [];

    var player = new Player(this, playerNamesInOrder[0], 10, 10, true);
    this.players.push(player);

    player = new Player(this, playerNamesInOrder[1], 10, 550, false);
    this.players.push(player);
    
    player = new Player(this, playerNamesInOrder[2], 700, 10, true);
    this.players.push(player);

    player = new Player(this, playerNamesInOrder[3], 700, 550, false);
    this.players.push(player);
}