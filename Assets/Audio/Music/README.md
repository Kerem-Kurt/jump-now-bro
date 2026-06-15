# Music assets

`music_loop.mp3` is a **silent placeholder**, committed so the game has a valid (silent) music
reference for anyone who clones the repo. The real licensed track is kept **out of git** on purpose
(licensing / future commercial use).

## Hearing the real track locally

Overwrite `music_loop.mp3` with your licensed track — **keep the filename** so the AudioManager's
music slot stays valid — then tell git to ignore your local copy so it is never committed:

    git update-index --skip-worktree "Assets/Audio/Music/music_loop.mp3"

To re-track it later: `git update-index --no-skip-worktree "Assets/Audio/Music/music_loop.mp3"`.

## Release builds

Have the real track in place before you build; Unity bakes the **assigned clip** into the build, not
your repo, so swapping the music right before a build (e.g. a Steam release) is a one-file change.
