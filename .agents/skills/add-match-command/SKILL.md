---
name: add-match-command
description: Thêm một thao tác người chơi (intent) mới trong trận đấu KLTN theo command pipeline server-authoritative.
---

# Thêm match command

Mẫu tham khảo gọn nhất: **Surrender** (`rg -n "RequestSurrender|SubmitSurrenderRpc|TrySurrender" Assets/Scripts`).

1. **Domain** — tạo `Assets/Scripts/Game/Domain/Match/MatchRulesEngine.<UseCase>.cs` (partial).
   `public CommandResult Try<UseCase>(MatchState state, SeatId actor, ...)`:
   guard null/finished → phase → priority → ownership → zone/mana/slot → mutate atomic → `CommandResult.Success(...)`.
   Lý do từ chối mới → thêm vào `CommandRejectionReason`. XML summary bắt buộc.
2. **Test Domain** — `Assets/Tests/EditMode/Domain/Match/`: case hợp lệ + mỗi lý do reject.
3. **Client** — `NetworkMatchBridge.ClientCommands.cs`: `public bool Request<UseCase>(...)` gửi instance ID + `nextLocalCommandId++`.
   Không gửi object card/transform.
4. **Server** — `NetworkMatchBridge.ServerCommands.cs`: `[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)] Submit<UseCase>Rpc(ulong commandId, ..., RpcParams)`
   → `TryBeginCommand(senderClientId, commandId, out actor)` → `rulesEngine.Try<UseCase>` → publish / reject.
   **Không mutate `MatchState` trực tiếp trong RPC.**
5. **Projection** — nếu có state mới cho client: thêm field DTO trong `Contracts/NetworkDtos.cs` và điền trong
   `MatchSnapshotBuilder` — tự hỏi: *đối thủ có được thấy field này không?*
6. **Presentation** — presenter gọi `bridge.Request<UseCase>()`, render từ projection, xử lý rejection từ inbox.
7. **Kiểm chứng** — EditMode pass; Multiplayer Play Mode host + guest; thử command trùng/cũ và sai phase.
