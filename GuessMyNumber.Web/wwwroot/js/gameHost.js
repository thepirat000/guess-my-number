function showGameAsHost(model) {
    $("#host-game-id").text(model.game.gameId);
    $("#host-number-container").text(model.gameNumber)
    $("#host-number").text("• ".repeat(model.game.digits).trim());

    setHostNextTurnOrWinner(model.game);
    setHostGameStatus(model.game);
    showHostButtons(model.game)
    showHostGameUsers(model.game);
    if (model.game.maxTries) {
        $("#host-tries-left").text("Tries left: " + model.triesLeft);
    } else {
        $("#host-tries-left").text("");
    }
    $("#host-tries-left").toggle(model.gameStatus === gameStatuses[1]);

    // Make tries grid
    let data = model.playerTries;
    $("#host-jsGrid").jsGrid({
        height: 200,
        width: "100%",
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
                return $("<td>").addClass("host-try-cell").append(item); } },
        { name: "number", title: "#", width: "15" },
        { name: "answer", title: "Answer", width: "20" }
        ],
        rowClick: function (args) {
            //selectedRowNotes = args.event.currentTarget.cells[0];
        }
    });

    $("#host-game-div").hide();
    $("#host-game-div").show('fast').css("display", "inline-block");;
};

function showHostButtons(game) {
    let showStart = game.status === 0 && game.players.some(p => p.role === 1 && p.status === 1);
    $("#host-start-button").prop('disabled', !showStart);
    let showAbandon = game.status === 1;
    $("#host-abandon-button").prop('disabled', !showAbandon);
}

function setHostGameStatus(game) {
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
    $("#host-status").text(statusText);
    $("#host-status").css('color', color);
    $("#host-tries-left").toggle(status === 1);
}

function addTryToHostedGame(number, playerId, tryNumber, answer) {
    $("#host-jsGrid").jsGrid("insertItem", { playerId: playerId, tryNumber: tryNumber, number: number, answer: answer }).done(function () {
    });
}

function setWinHostedGame(playGameResponse, number) {
    // Hosted game won
    setHostNextTurnOrWinner(playGameResponse.game);
    let msg = "You've won the game " + playGameResponse.game.gameId + " as host. Correct number was " + number;
    showPopupMessage("Winner", msg, true);
}
function setLoseHostedGame(playGameResponse, number) {
    // Hosted game loss
    setHostNextTurnOrWinner(playGameResponse.game);
    let msg = "You've lost the game " + playGameResponse.game.gameId + " against " + playGameResponse.playerId + ". Correct number was " + number + " guessed in " + playGameResponse.answer.tryNumber + " tries";
    showPopupMessage("Loser", msg, true);
}
function setAbandonedHostedGame(gameResponse) {
    // Hosted game loss
    setHostNextTurnOrWinner(gameResponse.game);
    let msg = "You've abandoned the game " + gameResponse.game.gameId;
    showPopupMessage("Loser", msg, true);
}

function setHostNextTurnOrWinner(game) {
    if (game.winnerPlayerId) {
        $("#host-next-turn").text("Winner: " + game.winnerPlayerId);
    }
    else {
        if (game.nextTurnPlayerId) {
            $("#host-next-turn").text("Turn: " + game.nextTurnPlayerId);
        } else {
            $("#host-next-turn").text("");
        }
        // tries left
        if (game.maxTries) {
            let tries = parseInt($(".host-try-cell:last").text())
            if (!isNaN(tries)) {
                $("#host-tries-left").text("Tries left: " + (game.maxTries - tries));
            } else {
                $("#host-tries-left").text("Tries left: " + game.maxTries);
            }
        } else {
            $("#host-tries-left").text("");
        }
    }
}

function showHostGameUsers(game) {
    let players = game.players.filter(p => p.role === 1 && p.status <= 3).map(p => p.playerId).join(', ');
    if (!players) {
        $("#host-game-users").html("<span style='color: darkgray;'>Waiting for users</span>");
    } else {
        $("#host-game-users").text(players);
    }
    
}


$(document).ready(function () {
    // add show number button
    $("#host-number").dblclick(function () {
        let number = $("#host-number-container").text();
        if ($("#host-number").text().replace(/ /g, '') === number) {
            $("#host-number").text("• ".repeat(number.length).trim());
        } else {
            $("#host-number").text(number.split('').join(' '));
        }
    });

    $("#host-start-button").click(function () {
        sendMessageToServer("/start " + getCurrentHostGame());
    });

    $("#host-abandon-button").click(function () {
        sendMessageToServer("/abandon " + getCurrentHostGame());
    });

    $("#host-invite-whatsapp-button").on('click', event => {
        let hostGameId = getCurrentHostGame();
        let firstName = $("#firstname").val();
        let link = getInviteLink(hostGameId);
        if (link) {
            let msg = "You've been invited to game " + hostGameId + " by " + firstName + ". Join with the following link: " + link;
            window.open('https://wa.me/?text=' + encodeURI(msg));
        }
        event.preventDefault();
    });
    $("#host-invite-link-button").on('click', event => {
        let hostGameId = getCurrentHostGame();
        let link = getInviteLink(hostGameId);
        if (link) {
            copyToClipboard(link);
            showPopupMessage('Invite', 'Link copied to clipboard: <div>' + link + '</div>', false);
        }
        event.preventDefault();
    });

    $("#new-game-maxtries").on("input", event => {
        $("#new-host-unlimited-tries").prop('checked', $("#new-game-maxtries").val().length === 0);
    });

    $("#new-host-unlimited-tries").on("change", event => {
        if ($("#new-host-unlimited-tries").prop('checked')) {
            $("#new-game-maxtries").val('');
        }
    });

    $("#new-host-unlimited-tries").on("focusout", event => {
        $("#new-host-unlimited-tries").prop('checked', $("#new-game-maxtries").val().length === 0);
    });

    $("#create-host-game-button").on('click', event => {
        let number = $("#new-game-number").val();
        let maxTries = $("#new-host-unlimited-tries").prop('checked') ? null : $("#new-game-maxtries").val();
        if (number) {
            let command = "/create " + number + " " + maxTries;
            sendMessageToServer(command.trim());
            $("#modal-new-host-game").modal("hide");
        }
        event.preventDefault();
    });

});

function getInviteLink(hostGameId) {
    if (hostGameId) {
        return window.location.origin + "/Game/Index?j=" + hostGameId;
    }
    return null;
}
