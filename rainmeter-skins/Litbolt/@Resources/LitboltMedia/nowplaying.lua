local iniPath = ''

local function trim(s)
  if not s then return '' end
  return (s:gsub('^%s+', ''):gsub('%s+$', ''))
end

local function readIni(path)
  local data = {}
  local f = io.open(path, 'r')
  if not f then return data end
  for line in f:lines() do
    local key, val = line:match('^([^=]+)=(.*)$')
    if key then
      data[trim(key)] = val
    end
  end
  f:close()
  return data
end

local function applyStayOnTop()
  local ontop = tonumber(SKIN:GetVariable('StayOnTop')) or 0
  if ontop == 1 then
    SKIN:Bang('!ZPos', 'OnTop')
    SKIN:Bang('!SetOption', 'MeterPin', 'FontColor', SKIN:GetVariable('Accent'))
  else
    SKIN:Bang('!ZPos', 'Normal')
    SKIN:Bang('!SetOption', 'MeterPin', 'FontColor', '150,150,160')
  end
end

function Initialize()
  iniPath = SKIN:GetVariable('NowPlayingIni')
  applyStayOnTop()
end

function TogglePin()
  local ontop = tonumber(SKIN:GetVariable('StayOnTop')) or 0
  local nextVal = 1 - ontop
  local settingsFile = SKIN:GetVariable('SettingsFile')
  local settingsPath = SKIN:GetVariable('SettingsPath')
  SKIN:Bang('!SetVariable', 'StayOnTop', tostring(nextVal))
  SKIN:Bang('!WriteKeyValue', 'Variables', 'StayOnTop', tostring(nextVal), settingsFile)
  SKIN:Bang('!WriteKeyValue', 'Litbolt\\Now Playing', 'AlwaysOnTop', tostring(nextVal), settingsPath)
  applyStayOnTop()
  SKIN:Bang('!Redraw')
end

function Update()
  if iniPath == '' or iniPath == nil then
    iniPath = SKIN:GetVariable('NowPlayingIni')
  end

  local mediaDir = SKIN:GetVariable('MediaDir')
  local d = readIni(iniPath)

  local title = d['Title'] or ''
  local artist = d['Artist'] or ''
  local state = tonumber(d['State'] or '0') or 0
  local progress = d['Progress'] or '0'
  local posText = d['PositionText'] or '0:00'
  local durText = d['DurationText'] or '0:00'
  local rev = d['CoverRev'] or '0'

  SKIN:Bang('!SetVariable', 'NpTitle', title)
  SKIN:Bang('!SetVariable', 'NpArtist', artist)
  SKIN:Bang('!SetVariable', 'NpState', tostring(state))
  SKIN:Bang('!SetVariable', 'NpProgress', progress)
  SKIN:Bang('!SetVariable', 'NpTime', posText .. ' / ' .. durText)
  SKIN:Bang('!SetVariable', 'NpCoverFile', mediaDir .. 'cover.jpg?' .. rev)

  local playing = (state == 1)
  if playing then
    SKIN:Bang('!ShowMeter', 'MeterPause')
    SKIN:Bang('!HideMeter', 'MeterPlay')
  else
    SKIN:Bang('!HideMeter', 'MeterPause')
    SKIN:Bang('!ShowMeter', 'MeterPlay')
  end

  return state
end
