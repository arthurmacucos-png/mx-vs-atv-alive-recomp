// mx_alive - ReXGlue Recompiled Project
//
// Customize your app by overriding virtual hooks from rex::ReXApp.

#pragma once

#include <rex/cvar.h>
#include <rex/logging.h>
#include <rex/rex_app.h>

#include "midasm_hooks.h"

class MxAliveApp : public rex::ReXApp {
 public:
  using rex::ReXApp::ReXApp;

  static std::unique_ptr<rex::ui::WindowedApp> Create(
      rex::ui::WindowedAppContext& ctx) {
    return std::unique_ptr<MxAliveApp>(new MxAliveApp(ctx, "mx_alive",
        PPCImageConfig));
  }

  void OnPreSetup(rex::RuntimeConfig& config) override {
    config.gpu_plugin = "xenos";
  }

  void OnPostLoadXexImage() override { mx_alive_dump_thunk_region(); }

  void OnPostSetup() override {
    REXLOG_INFO(
        "mx_alive: readback_resolve_async={} readback_resolve={} refresh_rate={} "
        "resolution_scale={} video_mode={}x{}",
        rex::cvar::GetFlagByName("d3d12_readback_resolve_async"),
        rex::cvar::GetFlagByName("readback_resolve"),
        rex::cvar::GetFlagByName("video_mode_refresh_rate"),
        rex::cvar::GetFlagByName("resolution_scale"),
        rex::cvar::GetFlagByName("video_mode_width"),
        rex::cvar::GetFlagByName("video_mode_height"));
  }

  // Override virtual hooks for customization:
  // void OnPostInitLogging() override {}
  // void OnPreSetup(rex::RuntimeConfig& config) override {}
  // void OnLoadXexImage(std::string& xex_image) override {}
  // void OnPostLoadXexImage() override {}
  // void OnPostSetup() override {}
  // void OnCreateDialogs(rex::ui::ImGuiDrawer* drawer) override {}
  // std::unique_ptr<rex::ui::ImGuiDialog> CreateAchievementsOverlay() override;
  // std::unique_ptr<rex::ui::AchievementNotificationDialog>
  // CreateAchievementNotificationDialog() override;
  // void OnShutdown() override {}
  // void OnConfigurePaths(rex::PathConfig& paths) override {}
};
