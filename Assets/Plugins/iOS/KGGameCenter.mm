// Native Game Center score submit for GameCenterService.SubmitDaily.
//
// Unity's Social.ReportScore goes through the old GKScore API, which predates
// recurring leaderboards. The daily challenge board is a recurring (daily reset)
// leaderboard, so it is posted through GKLeaderboard's iOS 14+ submit call instead.
// That call works for classic boards too. Sign-in is still done by Unity's Social
// API; both share the same GKLocalPlayer.

#import <GameKit/GameKit.h>

extern "C" void KG_GameCenterSubmitScore(const char* leaderboardId, long long score)
{
    if (leaderboardId == NULL) return;
    GKLocalPlayer* player = GKLocalPlayer.localPlayer;
    if (!player.isAuthenticated) return;

    NSString* ident = [NSString stringWithUTF8String:leaderboardId];
    [GKLeaderboard submitScore:(NSInteger)score
                       context:0
                        player:player
                leaderboardIDs:@[ident]
             completionHandler:^(NSError* error) {
        if (error != nil) NSLog(@"[Knight's Gauntlet] Game Center submit failed (%@): %@", ident, error);
    }];
}
