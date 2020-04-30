function showGameAsGuess(model) {
    $("#guess-game-id").text(model.game.gameId);
    $("#guess-digits").text(model.game.digits + " digits");
    $("#guess-host-player").text("Host: " + model.game.hostPlayer.playerId);
    setGuessNextTurnOrWinner(model.game);
    setGuessGameStatus(model.game);
    showGuessButtons(model.game);

    if (model.game.maxTries) {
        $("#guess-tries-left").text("Tries left: " + model.triesLeft);
    } else {
        $("#guess-tries-left").text("");
    }
    $("#guess-tries-left").toggle(model.gameStatus === gameStatuses[1]);

    // Make tries grid
    let data = model.playerTries;
    $("#guess-jsGrid").jsGrid({
        height: "auto",
        width: "100%",
        noDataContent: "<span style='color: darkgray; font-style: italic;'>No guesses</span>",
        autoload: true,
        inserting: false,
        editing: false,
        controller: {
            loadData: function () {
                return data;
            }
        },
        fields: [{ name: "playerId", title: "Player", width: "20" },
        { name: "tryNumber", title: "Try", width: "10", cellRenderer: function (item, value) {
                return $("<td>").addClass("guess-try-cell").append(item); } },
        { name: "number", title: "#", width: "15" },
        { name: "answer", title: "Answer", width: "20" }
        ],
        rowClick: function (args) {
            //selectedRowNotes = args.event.currentTarget.cells[0];
        }
    });

    $("#guess-game-div").hide();
    $("#guess-game-div").show('fast').css("display", "inline-block");;
};

function showGuessButtons(game) {
    let showStart = game.status === 0 && game.players.some(p => p.role === 1 && p.status === 0);
    $("#guess-start-button").prop('disabled', !showStart);
    let showAbandon = game.status === 1;
    $("#guess-abandon-button").prop('disabled', !showAbandon);
    let showPlay = game.status === 1; 
    $("#play-div").toggle(showPlay);
    $("#guess-play-button").attr('disabled', !showPlay);
    if (showPlay) {
        $("#play-number").val('');
        $("#play-number").attr('maxlength', game.digits);
        $("#play-number").focus();
    }
}

function setGuessGameStatus(game) {
    let status = game.status;
    let color = getGameStatusColor(status);
    let isWinner = false;
    if (game.status === 2) {
        // finished
        let currentUser = $("#username").val();
        if (game.winnerPlayerId === currentUser) {
            isWinner = true;
            color = "green";
        }
    }
    let statusText = status === 0 ? "New" : status === 1 ? "Playing" : isWinner ? "Winner" : "Loser";
    
    $("#guess-status-id").val(game.status);
    $("#guess-status").text(statusText);
    $("#guess-status").css('color', color);
    $("#guess-tries-left").toggle(status === 1);
}

function addTryToGuessedGame(number, playerId, tryNumber, answer, currentUser) {
    if (playerId !== currentUser) {
        tryNumber = tryNumber.split('').map(c => '?').join('');
    }
    $("#guess-jsGrid").jsGrid("insertItem", { playerId: playerId, tryNumber: tryNumber, number: number, answer: answer }).done(function () {
    });
}

function setWinGuessedGame(playGameResponse, number) {
    // Guessed game won
    setGuessNextTurnOrWinner(playGameResponse.game);
    let msg = "You've won the game " + playGameResponse.game.gameId + " as guesser in " + playGameResponse.answer.tryNumber + " tries. Correct number was " + number;
    showPopupMessage("Winner", msg, true, true);
}
function setLoseGuessedGame(playGameResponse) {
    // Guessed game loss
    setGuessNextTurnOrWinner(playGameResponse.game);
    let msg = "You've lost the game " + playGameResponse.game.gameId + " hosted by " + playGameResponse.game.hostPlayer.playerId + " winner is " + playGameResponse.game.winnerPlayerId;
    showPopupMessage("Loser", msg, true, true);
}
function setAbandonedGuessedGame(gameResponse) {
    // Guessed game loss
    setGuessNextTurnOrWinner(gameResponse.game);
    let msg = "You've abandoned the game " + gameResponse.game.gameId;
    showPopupMessage("Loser", msg, true, true);
}

function setGuessNextTurnOrWinner(game) {
    if (game.winnerPlayerId) {
        $("#guess-next-turn").text("Winner: " + game.winnerPlayerId);
    }
    else {
        if (game.nextTurnPlayerId) {
            $("#guess-next-turn").text("Turn: " + game.nextTurnPlayerId);
        } else {
            $("#guess-next-turn").text("");
        }
        // tries left
        if (game.maxTries) {
            let tries = parseInt($(".guess-try-cell:last").text())
            if (!isNaN(tries)) {
                $("#guess-tries-left").text("Tries left: " + (game.maxTries - tries));
            } else {
                $("#guess-tries-left").text("Tries left: " + game.maxTries);
            }
        } else {
            $("#guess-tries-left").text("");
        }
    }
    
}

$(document).ready(function () {
    $("#guess-start-button").click(function () {
        sendMessageToServer("/start " + getCurrentGuessGame());
    });

    $("#guess-abandon-button").click(function () {
        sendMessageToServer("/abandon " + getCurrentGuessGame());
    });

    $("#guess-play-button").click(function () {
        let number = $('#play-number').val();
        if (number && number.length === parseInt($('#play-number').attr('maxlength'))) {
            $('#play-number').val('');
            sendMessageToServer("/play " + getCurrentGuessGame() + " " + number);
        }
    });

    $('#play-number').keypress(function (e) {
        if (e.which === 13) {
            $('#guess-play-button').click();
            return false;
        }
    });

});