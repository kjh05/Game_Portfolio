# Project Name: Cluck_Cluck (Multiplayer Matchmaking P2P Party Game)
>> Uses
>> C#, Unity, Steamworks API.

# Project Overview: 
>> 개발 기간: 2026 - 현재
>> 개발 형태: 1인 개발 (기획, 백엔드/네트워크 로직, C# 코어 시스템, 3D 모델링, 오디오 프로듀싱)
>> 주요 특징: Steamworks API 연동을 통한 P2P 로비 매치메이킹 및 동시성 방지 네트워크 처리 구현

# 기술
>> 언어: C# 
>> 엔진: Unity 6.5, Steamworks API (Steam P2P Networking & Matchmaking)
>> 핵심:
    > Multi-threading & Thread-safety
    > Race Condition Prevention
    > Network Retry Logic & Timeout Handling
    > Memory & Unmanaged Resource Management (GCHandle, P/Invoke)

# Key Engineering & Backend System Focus
>> 동시성(Race Condition) 방지 및 Thread-Safe 네트워크 상태 관리
    > 멀티플레이어 매치메이킹 및 P2P 로비 진입 시 발생하는 비동기 응답 처리 과정에서 상태 경합(Race Condition)을 방지하도록 제어 로직을 구현
    > 매치 상태 전환 및 플레이어 데이터 동기화 과정에서 불필요한 스레드 대기 시간을 줄이고 스레드 안전성(Thread-Safety)을 보장
>> 네트워크 연동 신뢰성 확보 (Retry Logic)
    > 패킷 손실 및 스팀 P2P 세션 연결 불안정 상황을 고려하여 Retry 핸들러 구축 
    > 지연 시간(Latency) 및 연결 실패 시 예외를 안정적으로 캡처하고 재연결 프로세스를 수행하도록 작성
>> C# 메모리 및 네이티브 자원 관리 (GCHandle)
    > P/Invoke 및 네이티브 API 호출 과정에서 발생할 수 있는 메모리 누수를 방지하기 위해 GCHandle을 이용한 수동 자원 고정/해제 로직을 적용
    > GC(Garbage Collector)의 Overhead를 최소화하고 안정적인 프레임 유지와 메모리 발자국(Memory Footprint) 관리를 도모
>> System Architecture & Workflow
    > [Client] --> Steam Matchmaking API --> [Steam P2P Lobby]
         └--> (Async Event) --> [State Manager] <--┘
                                       └-> (Race Condition Guard) --> [Packet Processor & Retry Engine]
>> Repository Structure
    > Assets/
        ├── Scripts/        # C# Scripts, Networking, Matchmaking, Game Logic
        ├── Scenes/         # Build & Test Scenes
        └── Resources/      # Game Assets + Prefabs
    > Packages/             # Unity Package Settings
    > ProjectSettings/      # Project Settings
