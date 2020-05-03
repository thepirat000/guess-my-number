"use strict";

document.body.addEventListener('input', enforce_maxlength);

new ClipboardJS('.btn-copy-clipboard');

navigator.serviceWorker.register('/js/sw.js');

function enforce_maxlength(event) {
    var t = event.target;
    if (t.hasAttribute('maxlength')) {
        t.value = t.value.slice(0, t.getAttribute('maxlength'));
    }
}

const connection = new signalR.HubConnectionBuilder().withUrl("/gameHub").build();
const chat_history_entries = 50;
const chat_history_hours = 4;
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

function saveChatHistory() {
    // Store last messages on local storage
    let chatHistory = '';
    $('#messages li:gt(-' + (chat_history_entries + 1) + ')').each(function () { chatHistory += this.outerHTML; });
    localStorage.setItem('chat-history', JSON.stringify({ date: new Date(), html: chatHistory }));
}

function restoreChatHistory() {
    let chatHistory = localStorage.getItem('chat-history');
    if (chatHistory) {
        let ch = JSON.parse(chatHistory);
        let hoursDiff = (new Date() - new Date(ch.date)) / 1000 / 60 / 60;
        if (hoursDiff > 0 && hoursDiff <= chat_history_hours) {
            $("#messages").append(ch.html);
            scrollMessages();
        } else {
            localStorage.removeItem('chat-history');
        }
    }
}

function clearChatHistory() {
    $("#messages").empty();
    localStorage.removeItem('chat-history');
}

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
                    showGameButtons(gameResponse.game, true);
                } else if (gameResponse.game.gameId === getCurrentGuessGame()) {
                    // Current guessed game has started
                    setGuessGameStatus(gameResponse.game);
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
    loadHostGame(gameId, null);
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
    loadGuessGame(gameId, null);
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
    // - Clear chat window
    else if (message.toLowerCase().startsWith("/clear")) {
        clearChatHistory();
        return null;
    }

    return message;
}

function appendLine(user, line) {
    if (user) {
        line = "<span style='color: gray; font-style:italic;'>" + user + "</span> " + line;
    }
    let html = converter.makeHtml(line);
    let date = new Date();
    let formatDate = date.getFullYear() + "-" + (date.getMonth() < 9 ? "0" + (date.getMonth() + 1) : date.getMonth() + 1) + "-" + date.getDate() + " " + date.getHours() + ":" + (date.getMinutes() < 10 ? "0" + date.getMinutes() : date.getMinutes());
    $("<li>").attr('title', formatDate).tooltip({
        track: true,
        position: { my: "left left", at: "left left" }
    }).html(html).appendTo("#messages");
    saveChatHistory();
    let currentUser = $("#username").val();
    if (user && user !== currentUser) {
        let txt = $($.parseHTML(html)).text();
        if (txt.startsWith(user + " ")) {
            txt = txt.substring(user.length + 1);
        }
        notify(user, txt);
    }
    scrollMessages();
}

function getMessageText() {
    return $("#message").emojioneArea()[0].emojioneArea.getText()
}
function setMessageText(text) {
    return $("#message").emojioneArea()[0].emojioneArea.setText(text);
}
function setMessageTextFocus() {
    return $("#message").emojioneArea()[0].emojioneArea.setFocus();
}

$(document).ready(function () {
    $("#page-title").text($("#firstname").val());

    makeMessagesResizable();

    // Emojine area
    let emoji = $("#message").emojioneArea({
        pickerPosition: "top",
        tonesStyle: "bullet",
        useSprite: true,
        shortnames: true,
        container: "#emoji-container",
        events: {
            keydown: function (editor, e) {
                // If emoji autocomplete is showing, avoid processing the keyboard
                if ($(".textcomplete-dropdown:visible").length > 0) {
                    return;
                }
                if (e.keyCode === 13) {
                    $('#send').click();
                }
                else if (e.keyCode === 38) {
                    // up key
                    if (cmdStack.length) {
                        let cmd = cmdStack.pop();
                        cmdBackStack.push(cmd);
                        setMessageText(cmd);
                    }
                    return false;
                } else if (e.keyCode === 40) {
                    // down key
                    if (cmdBackStack.length) {
                        let cmd = cmdBackStack.pop();
                        cmdStack.push(cmd);
                        setMessageText(cmd);
                    }
                    return false;
                }
            }
        }
    });

    $('#messages').on("click", '.msg-start-game', function (e) {
        let gameId = $(this).data('game-id');
        sendMessageToServer("/start " + gameId);
        setMessageTextFocus();
        e.preventDefault();
    });
    $('#messages').on("click", '.msg-join-game', function (e) {
        let gameId = $(this).data('game-id');
        sendMessageToServer("/join " + gameId);
        setMessageTextFocus();
        e.preventDefault();
    });

    $(".host-close-button").on('click', event => {
        hideCurrentHostGame();
        setMessageTextFocus();
        event.preventDefault();
    });
    $(".guess-close-button").on('click', event => {
        hideCurrentGuessGame();
        setMessageTextFocus();
        event.preventDefault();
    });

    $("#send").on('click', event => {
        let message = emoji[0].emojioneArea.getText();
        setMessageText('').setFocus();
        if (message) {
            cmdStack.push(message);
            sendMessageToServer(message);
        }
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

    $("#player-stats-modal-button").on('click', event => {
        if (_connectedUsers) {
            showStatsPlayers(_connectedUsers.join(','));
        }
        event.preventDefault();
    });


    $("#new-game-modal-button").on('click', event => {
        showNewHostGamePopup();
        event.preventDefault();
    });

    let currentHostGame = getCurrentHostGame();
    if (currentHostGame) {
        // Load host game
        loadHostGame(currentHostGame, () => {
            setMessageTextFocus();
        });
    }
    let currentGuessGame = getCurrentGuessGame();
    if (currentGuessGame) {
        // Load guess game
        loadGuessGame(currentGuessGame, () => {
            setMessageTextFocus();
        });
    }

    restoreChatHistory();
    setMessageTextFocus();
    $("#messages-div").show();
    enableNotifications();
});

function makeMessagesResizable() {
    $("#messages-chat-div").resizable({
        minHeight: 160,
        handles: "s, se",
        //containment: "parent",
        resize: function (event, ui) {
            $(this).css("width", '');
        }
    });
}

function scrollMessages() {
    $('#messages').scrollTop($('#messages')[0].scrollHeight);
}

function loadHostGame(gameId, callback) {
    $.ajax("/Game/GameAsHost?gameId=" + gameId, {
        success: function (data) {
            showGameAsHost(data);
            if (callback) {
                callback();
            }
        },
        error: function (xhr, status, error) {
            hideCurrentHostGame();
            localStorage.removeItem('current-host-gameId');
            console.error(xhr.responseText);
            if (callback) {
                callback();
            }
        }
    });
}

function loadGuessGame(gameId, callback) {
    $.ajax("/Game/GameAsGuess?gameId=" + gameId, {
        success: function (data) {
            showGameAsGuess(data);
            if (callback) {
                callback();
            }
        },
        error: function (xhr, status, error) {
            hideCurrentGuessGame();
            localStorage.removeItem('current-guess-gameId');
            console.error(xhr.responseText);
            if (callback) {
                callback();
            }
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

function showPopupMessage(title, message, showNewGame, showPlayerStats) {
    $("#dialog-jsGrid").empty();
    $("#dialog-jsGrid").hide();
    $("#dialog-title").text(title);
    $("#dialog-body-text").html(message);
    $("#new-game-modal-button").toggle(showNewGame);
    $("#player-stats-modal-button").toggle(showPlayerStats);
    $("#modal-div").modal("show");
    $("#close-modal-button").focus();
}

function getGameStatusColor(status) {
    let color = status === 2 ? "red" : status === 1 ? "green" : "chocolate";
    return color;
}

function showNewHostGamePopup() {
    $("#new-game-number").val('');
    $("#modal-new-host-game").modal();
}

function enableNotifications() {
    if (!("Notification" in window)) {
        return;
    }
    // Let's check whether notification permissions have already been granted
    if (Notification.permission !== "granted" && Notification.permission !== "denied") {
        Notification.requestPermission();
    }
}
function canNotify() {
    if (!("Notification" in window)) {
        return false;
    }
    return Notification.permission === "granted";
}

function notify(title, msg) {
    if (document.body.className === "hidden") {
        if (canNotify()) {
            displayNotification(title, msg);
        }
    }
}

let lastNotificationShownDate = new Date(2000, 0, 1);

function displayNotification(title, body, link, duration) {
    link = link || null;
    duration = duration || 5000;
    let msDiff = new Date() - lastNotificationShownDate;
    if (msDiff < duration) {
        return;
    }
    let options = {
        body: body
    };
    if (platform.os.family === "Android") {
        navigator.serviceWorker.getRegistrations().then(function (registrations) { registrations[0].showNotification(title, options) })
    } else {
        let n = new Notification(title, options);
        if (link) {
            n.onclick = function (event) {
                event.preventDefault();
                window.open(link);
            };
        } else {
            n.onclick = function (event) {
                event.preventDefault();
                n.close.bind(n);
            };
        }
        if (duration) {
            setTimeout(n.close.bind(n), duration);
        }
    }
    lastNotificationShownDate = new Date();
}

// Visibility change, from https://stackoverflow.com/a/1060034/122195
(function () {
    var hidden = "hidden";
    // Standards:
    if (hidden in document)
        document.addEventListener("visibilitychange", onchange);
    else if ((hidden = "mozHidden") in document)
        document.addEventListener("mozvisibilitychange", onchange);
    else if ((hidden = "webkitHidden") in document)
        document.addEventListener("webkitvisibilitychange", onchange);
    else if ((hidden = "msHidden") in document)
        document.addEventListener("msvisibilitychange", onchange);
    // IE 9 and lower:
    else if ('onfocusin' in document)
        document.onfocusin = document.onfocusout = onchange;
    // All others:
    else
        window.onpageshow = window.onpagehide
            = window.onfocus = window.onblur = onchange;

    function onchange(evt) {
        var v = 'visible', h = 'hidden',
            evtMap = {
                focus: v, focusin: v, pageshow: v, blur: h, focusout: h, pagehide: h
            };

        evt = evt || window.event;
        if (evt.type in evtMap)
            document.body.className = evtMap[evt.type];
        else
            document.body.className = this[hidden] ? "hidden" : "visible";
        console.info(document.body.className)
    }
})();