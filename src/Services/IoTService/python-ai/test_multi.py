"""
test_multi.py — Script test nhanh endpoint /api/v1/stream/recognize_multi

Cách chạy:
  1. Terminal 1: python main.py --port 5001
  2. Terminal 2: python test_multi.py path/to/anh.jpg
     (hoặc không truyền gì để tự lấy 1 ảnh mẫu trong thư mục snapshots/)

Script sẽ:
  - Gửi ảnh lên server, in ra JSON kết quả (số xe phát hiện, biển số từng xe, ô đỗ nếu có)
  - Vẽ lại bounding box xe + biển số + text lên ảnh, lưu ra result_multi.jpg để bạn xem trực quan
"""
import base64
import json
import sys
import os
import glob
import requests
import cv2
import numpy as np

SERVER_URL = "http://127.0.0.1:5001/api/v1/stream/recognize_multi"


def pick_test_image():
    """Nếu không truyền path, tự lấy 1 ảnh có tên chứa 'in' trong snapshots/ để test cho nhanh."""
    candidates = sorted(glob.glob("snapshots/*.jpg"))
    if not candidates:
        raise FileNotFoundError("Không tìm thấy ảnh nào trong snapshots/. Hãy truyền path ảnh làm tham số.")
    return candidates[0]


def image_to_base64(path):
    with open(path, "rb") as f:
        return base64.b64encode(f.read()).decode("utf-8")


def example_slots(frame_w, frame_h):
    """
    Ví dụ 2 ô đỗ minh hoạ, chia đôi khung hình theo chiều ngang.
    THAY toạ độ này bằng toạ độ polygon thật của từng ô đỗ trên camera giám sát của bạn
    (vẽ 1 lần bằng cách click 4 góc mỗi ô trên 1 ảnh mẫu, ghi lại toạ độ pixel).
    """
    mid = frame_w // 2
    return [
        {"slot_id": "A1", "polygon": [[0, 0], [mid, 0], [mid, frame_h], [0, frame_h]]},
        {"slot_id": "A2", "polygon": [[mid, 0], [frame_w, 0], [frame_w, frame_h], [mid, frame_h]]},
    ]


def main():
    img_path = sys.argv[1] if len(sys.argv) > 1 else pick_test_image()
    print(f"[TEST] Dùng ảnh: {img_path}")

    # Dùng np.fromfile + cv2.imdecode thay vì cv2.imread trực tiếp vì cv2.imread
    # bị lỗi với đường dẫn chứa ký tự Unicode (tiếng Việt có dấu) trên Windows.
    try:
        with open(img_path, "rb") as f:
            file_bytes = np.frombuffer(f.read(), dtype=np.uint8)
        frame = cv2.imdecode(file_bytes, cv2.IMREAD_COLOR)
    except OSError as e:
        raise FileNotFoundError(f"Không mở được file: {img_path} ({e})")
    if frame is None:
        raise FileNotFoundError(f"Không đọc được ảnh (file lỗi hoặc không phải ảnh hợp lệ): {img_path}")
    h, w = frame.shape[:2]

    payload = {
        "image": image_to_base64(img_path),
        "slots": example_slots(w, h),  # bỏ dòng này (hoặc để []) nếu chưa muốn test slot-matching
        "debug": True,  # server sẽ lưu ảnh crop vào ./debug_output/ để kiểm tra bằng mắt
    }

    resp = requests.post(SERVER_URL, json=payload, timeout=60)
    print(f"[TEST] HTTP {resp.status_code}")
    data = resp.json()
    print(json.dumps(data, ensure_ascii=False, indent=2))

    if not data.get("success"):
        return

    # Vẽ lại kết quả lên ảnh để kiểm tra trực quan
    for v in data.get("vehicles", []):
        x1, y1, x2, y2 = map(int, v["vehicle_bbox"])
        label = v.get("plate") or "???"
        slot = v.get("slot_id")
        text = f"{label}" + (f" | {slot}" if slot else "")
        cv2.rectangle(frame, (x1, y1), (x2, y2), (0, 255, 0), 2)
        cv2.putText(frame, text, (x1, max(0, y1 - 8)),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 0), 2)

    out_path = "result_multi.jpg"
    cv2.imwrite(out_path, frame)
    print(f"[TEST] Đã lưu ảnh kết quả: {out_path} — mở lên xem bbox + biển số có đúng không.")


if __name__ == "__main__":
    main()