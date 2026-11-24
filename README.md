# Pente (Blazor WebAssembly, .NET 11)

```
dotnet run --project src/Pente.Web     # http://localhost:5199
dotnet test                            # rules + AI tests
```

- **Modes:** two players, or you vs the computer (Easy / Medium / Hard; play White or Black).
- **Rules:** 19×19, White opens on the center; win with 5+ in a row or 5 captured pairs.
  Optional tournament rule (White's 2nd stone ≥ 3 intersections from center).
- **Board:** mouse wheel zooms around the cursor, pinch zooms on touch, drag pans,
  +/−/reset buttons under the board. A click places a stone, but a drag never does.

## Layout
- `src/Pente.Core`: game model (`GameState`, `GameRules`) and AI (`Ai/PenteAi`, `Ai/Evaluator`), no UI dependencies.
- `src/Pente.Web`: UI. `Components/GameBoard.razor` draws the SVG board, and `GameBoard.razor.js` handles zoom, pan and placement by changing the SVG viewBox.
- `tests/Pente.Core.Tests`: xUnit tests.
