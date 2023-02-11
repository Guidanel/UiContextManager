# UiContextManager

A simple context system for UIs in Unity that I created for my game Cubi Code (https://giyomugames.com/cubi-code.html):
- You can make (most of) the UI flow without writing code
- If code is necessary you just need to call "ChangeContext(newContext)" on the appropriate UiContextManager
- Most modifications don't require changing the code since contexts rarely change and the set up is done in the scenes
- Automatically prevents players from clicking on buttons during contexts transitions

It is a very basic system and a lot can be improved (UX in particular) but most importantly it showcases the concept of managing UI through contexts. This is not a concept that I have invented, this is just my personal take on it.