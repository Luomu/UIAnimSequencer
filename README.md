# UIAnimSequencer

## Intro

A tool for building UI animation sequences with LeanTween.

Unity's animation system is not best suited for user interfaces. The workflow is somewhat fiddly (requires animation+controller setup, animation breaks if things are moved/renamed) and Unity themselves do not recommend using Animation on UI for performance reasons (https://www.youtube.com/watch?v=_wxitgdx-UI).
It is common to use tweening libraries (LeanTween, DotTween, HotTween) for programmatic UI animations. However using them requires programming skills and chaining complex tweens can get tedious since previews require recompilation. Hence this editor.

The inspiration for this tool comes from this presentation by Space Ape Games: https://youtu.be/4JoBw212Kyg?t=976.
The source code for their tool is not available, so all similarities are based on looking at that presentation very carefully :)

## License

MIT. See LICENSE.md.