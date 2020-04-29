"use strict";

document.body.addEventListener('input', enforce_maxlength);

new ClipboardJS('.btn-copy-clipboard');

function enforce_maxlength(event) {
    var t = event.target;
    if (t.hasAttribute('maxlength')) {
        t.value = t.value.slice(0, t.getAttribute('maxlength'));
    }
}

const connection = new signalR.HubConnectionBuilder().withUrl("/gameHub").build();
let converter = new showdown.Converter();
let cmdStack = [];
let cmdBackStack = [];
let users = [];
let gameStatuses = ['New', 'Started', 'Ended'];
let _connectedUsers;

connection.start()
    .then(function () {
        console.log("connected");

        // Execute post load command (if any)
        let postCommand = $("#postCommand").val();
        if (postCommand) {
            window.history.pushState({}, document.title, window.location.pathname);
            sendMessageToServer(postCommand);
            $("#postCommand").val('');
        }


        connection.invoke('getConnectionId')
            .then(function (connectionId) {
                sessionStorage.setItem('conectionId', connectionId);
                // Send the connectionId to controller
            }).catch(err => console.error(err.toString()));
    })
    .catch(err => console.error(err.toString()));

// Receive a message from a user
connection.on("ReceiveUserMessage", function (user, message) {
    appendLine(user, message);
});

// Receive a message from server
connection.on("ReceiveServerMessage", function (message) {
    appendLine(null, message);
});

function showStartMessage(game) {
    let gameId = game.gameId;
    appendLine(null, "<a target='_blank' href='start' class='msg-start-game' data-game-id='" + gameId + "'>--&gt;Start game **`" + game.gameId + "`**</a>");
}

function showGameButtons(game, isHost) {
    if (isHost) {
        showHostButtons(game);
    } else {
        showGuessButtons(game);
    }
}

function showJoinButton(game) {
    let gameId = game.gameId;
    appendLine(null, "<a target='_blank' href='join' class='msg-join-game' data-game-id='" + gameId + "'>--&gt;Join game **`" + game.gameId + "`**</a>");
}

// Receive a command executed by a user
connection.on("ReceiveCommand", function (user, commandName, parameters, commandResponse, gameResponse) {
    if (commandResponse.hasErrors) {
        return;
    }
    let currentUser = $("#username").val();
    let gameId = gameResponse.game.gameId;
    // Command issued by the current user
    switch (commandName) {
        case "Create": {
            if (currentUser === user) {
                let isServerGame = gameResponse.game.hostPlayer.playerId === "SERVER";
                if (isServerGame) {
                    // Server game created by this user (user joined as guesser)
                    setCurrentGuessGame(gameId);
                    // user can start 
                    showStartMessage(gameResponse.game);
                    showGameButtons(gameResponse.game, false);
                } else {
                    // Normal game created by this user as host (user is host)
                    setCurrentHostGame(gameId);
                }
            } else {
                // Normal or Server game created by a different user (user can join as guesser)
                showJoinButton(gameResponse.game);
            }
            break;
        }
        case "Join": {
            if (currentUser === user) {
                // Current user joined a game as a guesser
                setCurrentGuessGame(gameId);
            } else {
                if (gameResponse.game.gameId === getCurrentHostGame()) {
                    // A user joined as guesser on the current user hosted game
                    setHostNextTurnOrWinner(gameResponse.game);
                    showHostGameUsers(gameResponse.game);
                    if (gameResponse.game.status !== 1) {
                        // not started, user can start 
                        showStartMessage(gameResponse.game);
                        showGameButtons(gameResponse.game, true);
                    } else {
                        // started (join that triggers a start)
                        setHostGameStatus(gameResponse.game);
                    }
                } else if (gameResponse.game.gameId === getCurrentGuessGame()) {
                    // Another user joined as guesser on the current user guess game (multi-guesser)
                    setGuessNextTurnOrWinner(gameResponse.game);
                    showGameButtons(gameResponse.game, false);
                }
            }
            break;
        }
        case "Start": {
            if (gameResponse.game.status === 1) {
                // A game has started
                if (gameResponse.game.gameId === getCurrentHostGame()) {
                    // Current hosted game has started
                    setHostGameStatus(gameResponse.game);
                    showStartMessage(gameResponse.game);
                    showGameButtons(gameResponse.game, true);
                } else if (gameResponse.game.gameId === getCurrentGuessGame()) {
                    // Current guessed game has started
                    setGuessGameStatus(gameResponse.game);
                    showStartMessage(gameResponse.game);
                    showGameButtons(gameResponse.game, false);
                } else {
                    // Another game has started
                }
            } else if (currentUser === user && gameResponse.game.hostPlayer.playerId === currentUser) {
                // Current user has started a game as host (override current hosted game)
                setCurrentHostGame(gameResponse.game.gameId);
            }

            break;
        }
        case "Play": {
            let number = parameters[1];
            if (currentUser !== user && getCurrentHostGame() === gameResponse.game.gameId) {
                // Another player turn on current hosted game
                addTryToHostedGame(number, gameResponse.playerId, gameResponse.answer.tryNumber, gameResponse.answer.answerString);
                setHostGameStatus(gameResponse.game);
                showGameButtons(gameResponse.game, true);
            } else if (getCurrentGuessGame() === gameResponse.game.gameId) {
                // player turn on current guess game
                addTryToGuessedGame(number, gameResponse.playerId, gameResponse.answer.tryNumber, gameResponse.answer.answerString, currentUser);
                setGuessGameStatus(gameResponse.game);
                showGameButtons(gameResponse.game, false);
            }

            if (gameResponse.game.status === 2) {
                if (getCurrentGuessGame() === gameResponse.game.gameId) {
                    // Current guessed game has finished
                    if (gameResponse.game.winnerPlayerId === currentUser) {
                        // Guesser wins
                        setWinGuessedGame(gameResponse, number);
                    } else if (gameResponse.game.winnerPlayerId) {
                        // Guesser loses
                        setLoseGuessedGame(gameResponse);
                    } else {
                        // No winner/loser
                    }
                } else if (getCurrentHostGame() === gameResponse.game.gameId) {
                    // Current hosted game has finished
                    if (gameResponse.game.winnerPlayerId === currentUser) {
                        // Host wins
                        setWinHostedGame(gameResponse, $("#host-number-container").text());
                    } else if (gameResponse.game.winnerPlayerId) {
                        // Host loses
                        setLoseHostedGame(gameResponse, number);
                    } else {
                        // No winner/loser
                    }
                }
            } else {
                // Normal turn
                if (currentUser === user && getCurrentGuessGame() === gameResponse.game.gameId) {
                    // Current player turn on current guess game
                    setGuessNextTurnOrWinner(gameResponse.game);
                } else if (currentUser !== user && getCurrentGuessGame() === gameResponse.game.gameId) {
                    // Another player turn on current guess game (multi-guesser game)
                    setGuessNextTurnOrWinner(gameResponse.game);
                } else if (currentUser !== user && getCurrentHostGame() === gameResponse.game.gameId) {
                    // Another player turn on current hosted game
                    setHostNextTurnOrWinner(gameResponse.game);
                }
            }
            break;
        }
        case "Abandon": {
            if (currentUser === user && getCurrentGuessGame() === gameResponse.game.gameId) {
                if (gameResponse.game.status === 2) {
                    // Current player abandoned guessed game, and game has finished
                    setGuessGameStatus(gameResponse.game);
                    setGuessNextTurnOrWinner(gameResponse.game);
                    showGameButtons(gameResponse.game, false);
                    setAbandonedGuessedGame(gameResponse);
                }
            } else if (currentUser === user && getCurrentHostGame() === gameResponse.game.gameId) {
                // Current player abandoned hosted game
                setHostGameStatus(gameResponse.game);
                setHostNextTurnOrWinner(gameResponse.game);
                showGameButtons(gameResponse.game, true);
                setAbandonedHostedGame(gameResponse);
            } else {
                if (getCurrentGuessGame() === gameResponse.game.gameId) {
                    // someone else abandoned the guessed game
                    setGuessGameStatus(gameResponse.game);
                    setGuessNextTurnOrWinner(gameResponse.game);
                    showGameButtons(gameResponse.game, true);
                } else if (getCurrentHostGame() === gameResponse.game.gameId) {
                    // someone else abandoned the hosted game
                    setHostGameStatus(gameResponse.game);
                    setHostNextTurnOrWinner(gameResponse.game);
                    showGameButtons(gameResponse.game, true);
                    setWinHostedGame(gameResponse, $("#host-number-container").text());
                }
            }

            break;
        }
        case "Cancel": {

            break;
        }
    }

});

function setCurrentHostGame(gameId) {
    loadHostGame(gameId);
    localStorage.setItem('current-host-gameId', gameId);
}
function hideCurrentHostGame() {
    if ($("#host-status-id").val() === "2") {
        localStorage.removeItem('current-host-gameId');
    }
    $("#host-game-div").hide('fast');
}
function getCurrentHostGame() {
    return localStorage.getItem('current-host-gameId');
}
function setCurrentGuessGame(gameId) {
    loadGuessGame(gameId);
    localStorage.setItem('current-guess-gameId', gameId);
}
function hideCurrentGuessGame() {
    if ($("#guess-status-id").val() === "2") {
        localStorage.removeItem('current-guess-gameId');
    }
    $("#guess-game-div").hide('fast');
}
function getCurrentGuessGame() {
    return localStorage.getItem('current-guess-gameId');
}


// Receive a user connected list change
connection.on("UserListChanged", function (connectedUsers) {
    _connectedUsers = connectedUsers;
    if (users.join() !== connectedUsers.join()) {
        if (connectedUsers.length) {
            $("#connected-users").html("<li>@" + connectedUsers.join("</li><li>@") + "</li>");
        }
        else {
            $("#connected-users").clear();
        }
        users = connectedUsers;
    }
});

function sendMessageToServer(message) {
    message = preProcessMessage(message);
    if (message) {
        connection.invoke("SendMessage", message).catch(function (err) {
            return console.error(err.toString());
        });
    }
}

function preProcessMessage(message) {
    // If enters a number of same digits as the current guess game, assume is a play command
    let currentGuessGame = getCurrentGuessGame();
    if (currentGuessGame) {
        if (parseInt($("#guess-digits").text()) === message.length) {
            if (/^\d+$/.test(message)) {
                message = "/play " + currentGuessGame + " " + message;
            }
        }
    }

    // Client commands
    // - Load a game as guesser
    if (message.toLowerCase().startsWith("/guess ")) {
        let split = message.split(" ");
        if (split.length > 1) {
            setCurrentGuessGame(split[1].toUpperCase());
        }
        return null;
    }
    // - Load a game as host
    else if (message.toLowerCase().startsWith("/host ")) {
        let split = message.split(" ");
        if (split.length > 1) {
            setCurrentHostGame(split[1].toUpperCase());
        }
        return null;
    }

    return message;
}

function appendLine(user, line) {
    let li = document.createElement('li');
    let html = converter.makeHtml(line);
    li.innerHTML = html;
    document.getElementById('messages').appendChild(li);
    $('#messages').scrollTop($('#messages')[0].scrollHeight);
}

function clearMessages() {
    $("messages").clear();
}

$(document).ready(function () {
    $("#page-title").text($("#firstname").val());

    $('#message').keypress(function (e) {
        if (e.which === 13) {
            $('#send').click();
            return false;
        } 
    });
    $('#message').keydown(function (e) {
        if (e.keyCode === 38) {
            // up key
            if (cmdStack.length) {
                let cmd = cmdStack.pop();
                cmdBackStack.push(cmd);
                document.getElementById('message').value = cmd;
            }
            return false;
        } else if (e.keyCode === 40) {
            // down key
            if (cmdBackStack.length) {
                let cmd = cmdBackStack.pop();
                cmdStack.push(cmd);
                document.getElementById('message').value = cmd;
            }
            return false;
        }
    });

    $('#messages').on("click", '.msg-start-game', function (e) {
        let gameId = $(this).data('game-id');
        sendMessageToServer("/start " + gameId);
        $('#message').focus();
        e.preventDefault();
    });
    $('#messages').on("click", '.msg-join-game', function (e) {
        let gameId = $(this).data('game-id');
        sendMessageToServer("/join " + gameId);
        $('#message').focus();
        e.preventDefault();
    });

    $(".host-close-button").on('click', event => {
        hideCurrentHostGame();
        event.preventDefault();
    });
    $(".guess-close-button").on('click', event => {
        hideCurrentGuessGame();
        event.preventDefault();
    });

    $("#send").on('click', event => {
        let message = document.getElementById('message').value;
        document.getElementById('message').value = '';
        if (message) {
            cmdStack.push(message);
            sendMessageToServer(message);
        }
        $("#message").removeAttr('placeholder');
        event.preventDefault();
    });

    $("#header-btn-guess").on('click', event => {
        showPopupGame('guess', 'Current guessing games');
        event.preventDefault();
    });
    $("#header-btn-host").on('click', event => {
        showPopupGame('host', 'Current hosted games');
        event.preventDefault();
    });
    $("#header-btn-stats").on('click', event => {
        if (_connectedUsers) {
            showStatsPlayers(_connectedUsers.join(','));
        }
        event.preventDefault();
    });

    $("#new-game-modal-button").on('click', event => {
        showNewHostGamePopup();
        event.preventDefault();
    });

    $('#message').focus();

    let currentHostGame = getCurrentHostGame();
    if (currentHostGame) {
        // Load host game
        loadHostGame(currentHostGame);
    }
    let currentGuessGame = getCurrentGuessGame();
    if (currentGuessGame) {
        // Load guess game
        loadGuessGame(currentGuessGame);
    }

});

function loadHostGame(gameId) {
    $.ajax("/Game/GameAsHost?gameId=" + gameId, {
        success: function (data) {
            showGameAsHost(data);
        },
        error: function (xhr, status, error) {
            hideCurrentHostGame();
            localStorage.removeItem('current-host-gameId');
            console.error(xhr.responseText);
        }
    });
}

function loadGuessGame(gameId) {
    $.ajax("/Game/GameAsGuess?gameId=" + gameId, {
        success: function (data) {
            showGameAsGuess(data);
        },
        error: function (xhr, status, error) {
            hideCurrentGuessGame();
            localStorage.removeItem('current-guess-gameId');
            console.error(xhr.responseText);
        }
    });
}

function showStatsPlayers(players) {
    $("#dialog-body-text").text("");
    $("#dialog-jsGrid").empty();
    $("#dialog-jsGrid").hide();
    $("#new-game-modal-button").hide();
    $.ajax("/Game/Stats?players=" + players, {
        success: function (data) {
            showPopupPlayerStats(data);
        },
        error: function (xhr, status, error) {
            console.error(xhr.responseText);
        }
    });
}

function showPopupMessage(title, message, showNewGame) {
    $("#dialog-jsGrid").empty();
    $("#dialog-jsGrid").hide();
    $("#dialog-title").text(title);
    $("#dialog-body-text").html(message);
    $("#new-game-modal-button").toggle(showNewGame);
    $("#modal-div").modal("show");
    $("#close-modal-button").focus();
}

function getGameStatusColor(status) {
    let color = status === 2 ? "red" : status === 1 ? "green" : "chocolate";
    return color;
}

function showNewHostGamePopup() {
    $("#new-game-number").val('');
    $("#modal-new-host-game").modal("show");
}