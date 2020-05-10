"use strict";

function showPopupPlayerStats(data) {
    $("#stats-dialog-body-text").html("");
    $("#stats-dialog-jsGrid").empty();
    
    let currentUser = $("#username").val();

    let gridData = [...data];
    gridData.forEach(g => {
        g.wonAsGuess = g.wonAsGuess === 0 ? '-' : g.wonAsGuess;
        g.lostAsGuess = g.lostAsGuess === 0 ? '-' : g.lostAsGuess;
        g.wonAsHost = g.wonAsHost === 0 ? '-' : g.wonAsHost;
        g.lostAsHost = g.lostAsHost === 0 ? '-' : g.lostAsHost;
        g.totalWon = g.totalWon === 0 ? '-' : g.totalWon;
        g.totalLost = g.totalLost === 0 ? '-' : g.totalLost;
    });

    $("#stats-dialog-jsGrid").jsGrid("destroy");
    $("#stats-dialog-jsGrid").jsGrid({
        height: "auto",
        width: "100%",
        autoload: true,
        inserting: false,
        editing: false,
        controller: {
            loadData: function () {
                return gridData;
            }
        },
        fields: [
            { name: "playerId", title: "Player", width: "25", cellRenderer: function (item, value) {
                    return $("<td>").css('font-weight', currentUser === value.playerId ? 'bold' : 'normal').append(item);
            }},
            { name: "wonAsGuess", title: "Win (guess)", width: "20", css: "win-count-small", headercss: "normal-text", align: "center" },
            { name: "lostAsGuess", title: "Lost (guess)", width: "20", css: "lost-count-small", headercss: "normal-text", align: "center" },
            { name: "wonAsHost", title: "Win (host)", width: "20", css: "win-count-small", headercss: "normal-text", align: "center" },
            { name: "lostAsHost", title: "Lost (host)", width: "20", css: "lost-count-small", headercss: "normal-text", align: "center" },
            { name: "totalWon", title: "Win (total)", width: "20", css: "win-count", headercss: "normal-text", align: "center"  },
            { name: "totalLost", title: "Lost (total)", width: "20", css: "lost-count", headercss: "normal-text", align: "center" },
            { name: "abandons", title: "Abandons", width: "20", css: "lost-count", headercss: "normal-text", align: "center" },
            { name: "triesAverage", title: "Tries (avg.)", width: "20", align: "center" }
        ],
    });
    $("#stats-dialog-jsGrid").show();
    $("#stats-modal-div").modal("show");

}

function showPopupGame(popupType, text) {
    $("#dialog-title").text(popupType === "host" ? "Host games" : "Guess games");
    $("#dialog-body-text").html(text);
    $("#dialog-jsGrid").empty();
    $("#new-game-modal-button").show();

    let currentUser = $("#username").val();

    // Query for joinable and playing games
    $.ajax("/Game/GetLoadableGames?type=" + popupType, {
        success: function (data) {

            let gridData = [...data];
            gridData.forEach(g => {
                let isJoinedAsGuesser = g.players.some(p => p.playerId === currentUser && p.role === 1);
                g.statusText = gameStatuses[g.status];
                g.statusColor = getGameStatusColor(gameStatuses[g.status]);
                g.command = g.hostPlayer.playerId === currentUser ? "host" : g.status === 0 && !isJoinedAsGuesser ? "join" : "guess";
                g.timeAgo = timeAgo(g.createdOn);
                g.action = g.hostPlayer.playerId === currentUser ? "host" : "guess";
            });

            $("#dialog-jsGrid").jsGrid("destroy");
            $("#dialog-jsGrid").jsGrid({
                height: 200,
                width: "100%",
                autoload: true,
                inserting: false,
                editing: false,
                controller: {
                    loadData: function () {
                        return gridData;
                    }
                },
                fields: [
                    { name: "gameId", title: "Game", width: "15", css: "monospaced-text", headercss: "normal-text" },
                    { name: "timeAgo", title: "Created", width: "27", css: "small-text", headercss: "normal-text" },
                    { name: "hostPlayer.playerId", title: "Host", width: "23", cellRenderer: function (item, value) {
                        return $("<td>").css('font-weight', (value.hostPlayer && value.hostPlayer.playerId === currentUser) ? "bold" : "normal").append(item);
                        }
                    },
                    { name: "digits", title: "Digits", width: "7", align: "center" },
                    { name: "maxTries", title: "Max tries", width: "7", align: "center" },
                    {
                        name: "statusText", title: "Status", width: "20", cellRenderer: function (item, value) {
                            return $("<td>").css('color', value.statusColor).append(item);
                        }
                    },
                    {
                        name: "action", title: "Action", width: "30", cellRenderer: function (item, value) {
                            return $("<td>").addClass(value.command + "-game-cell").append("<a class='game-link' href='" + value.command + "' target='_blank' data-game-id='" + value.gameId + "' data-command='" + value.command + "'>" + value.action.substring(0, 1).toUpperCase() + value.action.substring(1) + "</a>");
                        }
                    }
                ],
                rowClick: function (args) {
                    //selectedRowNotes = args.event.currentTarget.cells[0];
                },
                rowDoubleClick: function (args) {
                    $(args.event.currentTarget).find(".game-link").click();
                }
            });

            $("#dialog-jsGrid").show();
            $("#modal-div").modal("show");

        },
        error: function (xhr, status, error) {
            console.error(xhr.responseText);
        }
    });
}

$(document).ready(function () {
    $('#dialog-jsGrid').on("click", '.game-link', function (e) {
        let gameId = $(e.currentTarget).data('game-id');
        let command = $(e.currentTarget).data('command');
        sendMessageToServer("/" + command + " " + gameId);

        $("#modal-div").modal("hide");

        $('#message').focus();
        e.preventDefault();
    });
});