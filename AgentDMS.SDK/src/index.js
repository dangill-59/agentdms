const { app, BrowserWindow, Menu, ipcMain, dialog } = require('electron');
const path = require('path');
const { AgentDMSAPI } = require('./api/agentdms-api');

// Keep a global reference of the window object
let mainWindow;
let agentDMSAPI;

function createWindow() {
  // Create the browser window
  mainWindow = new BrowserWindow({
    width: 1200,
    height: 800,
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      enableRemoteModule: false,
      preload: path.join(__dirname, 'renderer', 'preload.js')
    },
    icon: path.join(__dirname, 'assets', 'icon.png')
  });

  // Load the app
  mainWindow.loadFile(path.join(__dirname, 'renderer', 'index.html'));

  // Initialize AgentDMS API
  agentDMSAPI = new AgentDMSAPI();

  // Development tools
  if (process.env.NODE_ENV === 'development') {
    mainWindow.webContents.openDevTools();
  }

  // Set up menu
  createMenu();

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

function createMenu() {
  const template = [
    {
      label: 'File',
      submenu: [
        {
          label: 'Open File',
          accelerator: 'CmdOrCtrl+O',
          click: () => {
            mainWindow.webContents.send('menu-open-file');
          }
        },
        {
          label: 'Scan Document',
          accelerator: 'CmdOrCtrl+S',
          click: () => {
            mainWindow.webContents.send('menu-scan-document');
          }
        },
        { type: 'separator' },
        {
          label: 'Exit',
          accelerator: process.platform === 'darwin' ? 'Cmd+Q' : 'Ctrl+Q',
          click: () => {
            app.quit();
          }
        }
      ]
    },
    {
      label: 'View',
      submenu: [
        {
          label: 'Zoom In',
          accelerator: 'CmdOrCtrl+Plus',
          click: () => {
            mainWindow.webContents.send('menu-zoom-in');
          }
        },
        {
          label: 'Zoom Out',
          accelerator: 'CmdOrCtrl+-',
          click: () => {
            mainWindow.webContents.send('menu-zoom-out');
          }
        },
        {
          label: 'Reset Zoom',
          accelerator: 'CmdOrCtrl+0',
          click: () => {
            mainWindow.webContents.send('menu-zoom-reset');
          }
        }
      ]
    },
    {
      label: 'Tools',
      submenu: [
        {
          label: 'Annotate',
          accelerator: 'CmdOrCtrl+A',
          click: () => {
            mainWindow.webContents.send('menu-toggle-annotation');
          }
        },
        {
          label: 'Upload to Server',
          accelerator: 'CmdOrCtrl+U',
          click: () => {
            mainWindow.webContents.send('menu-upload');
          }
        }
      ]
    }
  ];

  const menu = Menu.buildFromTemplate(template);
  Menu.setApplicationMenu(menu);
}

// App event handlers
app.whenReady().then(createWindow);

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    createWindow();
  }
});

// IPC handlers
ipcMain.handle('dialog:openFile', async () => {
  const result = await dialog.showOpenDialog(mainWindow, {
    properties: ['openFile'],
    filters: [
      { name: 'Images', extensions: ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'tiff', 'webp'] },
      { name: 'PDFs', extensions: ['pdf'] },
      { name: 'All Files', extensions: ['*'] }
    ]
  });
  return result;
});

ipcMain.handle('api-get-formats', async () => {
  return await agentDMSAPI.getSupportedFormats();
});

ipcMain.handle('api-get-scanners', async () => {
  return await agentDMSAPI.getAvailableScanners();
});

ipcMain.handle('api-scan-document', async (event, options) => {
  return await agentDMSAPI.scanDocument(options);
});

ipcMain.handle('api-upload-file', async (event, filePath, options) => {
  return await agentDMSAPI.uploadFile(filePath, options);
});

ipcMain.handle('api-process-file', async (event, filePath, options) => {
  return await agentDMSAPI.processFile(filePath, options);
});

ipcMain.handle('set-api-base-url', async (event, url) => {
  agentDMSAPI.setBaseUrl(url);
  return true;
});