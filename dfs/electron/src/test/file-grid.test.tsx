
//import React from "react";
import { render, screen, fireEvent, act } from "@testing-library/react";
import { FileGrid } from "../components/file-grid"; // Adjust path as needed
import type { File, Folder } from "@/lib/types";
/**
 * @jest-environment jsdom
 */

jest.mock("../components/menus/file-action-menu", () => ({
  FileActionMenu: () => <div data-testid="file-action-menu" />,
}));

jest.mock("../components/menus/folder-action-menu", () => ({
  FolderActionMenu: () => <div data-testid="folder-action-menu" />,
}));

const mockFolders: Folder[] = [
  {
    id: "folder-1",
    name: "Folder One",
    containerGuid: "guid-1",
    parentId: [],
    createdAt: "2024-01-01",
    modifiedAt: "2024-01-02",
    hasChildren: false,
  },
];

const mockFiles: File[] = [
  {
    id: "file-1",
    name: "Doc",
    containerGuid: "guid-1",
    parentId: [],
    createdAt: "2024-01-01",
    modifiedAt: "2024-01-02",
    type: "document",
    size: 1234,
  },
  {
    id: "file-2",
    name: "Sheet",
    containerGuid: "guid-1",
    parentId: [],
    createdAt: "2024-01-01",
    modifiedAt: "2024-01-02",
    type: "spreadsheet",
    size: 2048,
  },
  {
    id: "file-3",
    name: "Image",
    containerGuid: "guid-1",
    parentId: [],
    createdAt: "2024-01-01",
    modifiedAt: "2024-01-02",
    type: "image",
    size: 4096,
    thumbnail: "data:image/png;base64,xyz",
  },
  {
    id: "file-4",
    name: "Unknown",
    containerGuid: "guid-1",
    parentId: [],
    createdAt: "2024-01-01",
    modifiedAt: "2024-01-02",
    type: "other",
    size: 512,
  },
];

describe("FileGrid", () => {
  it("renders empty state", () => {
    render(
      <FileGrid
        files={[]}
        folders={[]}
        currentFolder={null}
        currentFolderName="Root"
        navigateToFolder={jest.fn()}
        navigateToParent={jest.fn()}
      />,
    );

    expect(screen.getByText("This folder is empty")).toBeInTheDocument();
  });

  it("renders folders and triggers navigation",async () => {
    const navigateToFolder = jest.fn();
    render(
      <FileGrid
        files={[]}
        folders={mockFolders}
        currentFolder={"some-id"}
        currentFolderName="Test Folder"
        navigateToFolder={navigateToFolder}
        navigateToParent={jest.fn()}
      />,
    );
await act(async () => {
  fireEvent.click(screen.getByText("Folder One"));
   });
    expect(navigateToFolder).toHaveBeenCalledWith("folder-1");
    expect(screen.getByTestId("folder-action-menu")).toBeInTheDocument();
  });

  it("renders different file types and action menus", () => {
    render(
      <FileGrid
        files={mockFiles}
        folders={[]}
        currentFolder={null}
        currentFolderName="Test Files"
        navigateToFolder={jest.fn()}
        navigateToParent={jest.fn()}
      />,
    );

    mockFiles.forEach((file) => {
      expect(screen.getByText(file.name)).toBeInTheDocument();
    });

    expect(screen.getAllByTestId("file-action-menu").length).toBe(4);
    expect(screen.getByRole("img")).toHaveAttribute("src", "data:image/png;base64,xyz");
  });

  it("calls navigateToParent when Back is clicked",async () => {
    const navigateToParent = jest.fn();
    render(
      <FileGrid
        files={[]}
        folders={[]}
        currentFolder={"some-folder-id"}
        currentFolderName="Some Folder"
        navigateToFolder={jest.fn()}
        navigateToParent={navigateToParent}
      />,
    );
   await act(async () => {
     fireEvent.click(screen.getByText("Back"));
   });
    expect(navigateToParent).toHaveBeenCalled();
  });
});
