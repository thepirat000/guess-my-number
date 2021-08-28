## Commands

### Server commands

#### `/Create` 

> Creates a new game as a host

- Syntax:
    - `/create {number} {auto-start} [{max-tries}]`

- Parameters:
    - `{number}`: The number to be guessed, or a number of asterisks (as many as digits wanted) to generate a random number.
    - `{auto-start}`: 0 or 1 to indicate if the game should start as soon as the first guesser joins. 
    - `{max-tries}`: (optional) The maximum number of tries before losing the game.

#### `/Join`

> Joins an existing game as a guesser

- Syntax:
    - `/join {game}`

- Parameters:
    - `{game}`: The game ID to join.

#### `/Start`

> Starts a game that is being hosted 

- Syntax:
    - `/start {game}`

- Parameters:
    - `{game}`: The game ID to start.

#### `/Play`

> Plays a turn in a game as guesser

- Syntax:
    - `/play {game} {number}`

- Parameters:
    - `{game}`: The game ID to play.
    - `{number}`: The guessing number.

#### `/Abandon`

> Abandons a currently playing game

- Syntax:
    - `/abandon {game}`

- Parameters:
    - `{game}`: The playing game ID to abandon.

### Client commands

#### `/Guess`

> Loads and set the currently guessing game

- Syntax:
    - `/guess {game}`

- Parameters:
    - `{game}`: The existing game ID to load as guesser.

#### `/Host`

> Loads and set the currently hosting game

- Syntax:
    - `/guess {game}`

- Parameters:
    - `{game}`: The existing game ID to load as host.

#### `/Clear`
      
> Clears the messages window

- Syntax:
    - `/clear`
