# Rule — Architectural Styles

> Style trả lời: **toàn hệ thống có hình dạng/topology gì?** (khác với pattern = phân vai module, và design pattern = cộng tác class).

## 1. Modular Monolith (primary)

Một ứng dụng Unity build chung gameplay, networking, content, presentation; chia module có dependency rule.

Quy tắc:
- Ranh giới module được **bảo vệ bằng asmdef**, không chỉ bằng folder. Hiện chỉ Domain có asmdef riêng
  (`KLTN.Game.Domain`, `noEngineReferences: true`). Phần còn lại nằm trong `Assembly-CSharp` → review thủ công.
- Không gọi tắt xuyên module (VD: Presentation gọi `MatchRulesEngine` trực tiếp, Content tham chiếu Presentation).
- Muốn thêm module độc lập (VD: Application layer) → tạo asmdef mới, khai báo reference một chiều.

Vì sao không microservice: game 1v1, state authoritative nằm trong 1 process là đơn giản và deterministic nhất.

## 2. Layered organization (supporting)

`Domain` (trong cùng) ← `Content`, `Networking` ← `Presentation` / `Infrastructure` / `UI` (ngoài cùng).

Quy tắc:
- Phụ thuộc chỉ hướng vào trong. Domain không biết tầng nào khác.
- Không bắt buộc request đi tuần tự qua mọi tầng (không phải strict layering); adapter được gọi use case phù hợp trực tiếp.

## 3. Client–Server, Server Authoritative (runtime topology)

- Server (host listen hoặc dedicated trên Edgegap) giữ `MatchState` duy nhất và chạy `MatchRulesEngine`.
- Client gửi intent + stable instance ID, nhận snapshot đã lọc + resolution DTO.

Hai topology cùng tồn tại:

| Topology | Đường code | Dùng khi | Giới hạn |
|---|---|---|---|
| Listen server (host-authoritative) qua UGS Session/Relay | `Infrastructure/Networking/Listen`, `Networking/UgsSessionService`, scene `HostClientMenu` | Dev/test, chơi với bạn bằng join code | Host process thấy toàn bộ state → không che thông tin tuyệt đối với host |
| Dedicated server trên **Edgegap** qua Matchmaker | `Infrastructure/Networking/Dedicated`, scene `FindMatch` / `ServerBootstrap` | Matchmaking công khai | Chi phí hosting, nhiều case lifecycle hơn, server hiện chưa xác thực client |

Quy tắc:
- Logic gameplay **không được phân nhánh theo topology**. `NetworkMatchBridge` chỉ hỏi `IsServer`/`IsClient`.
- Code đặc thù topology chỉ nằm trong `Infrastructure/Networking/{Listen,Dedicated}`.
- Command là cách biểu diễn intent **bên trong** topology này; đừng nhầm "Client–Server" với Command pattern.
