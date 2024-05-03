# Directories
SRC_DIR = .
OBJ_DIR = obj
BIN_DIR = bin
INC_DIR = ../common

# Compiler options
CC = gcc
CFLAGS = -Wall -Wextra -Werror -pedantic -std=c99 -Wno-unused-parameter -I$(INC_DIR)
LDFLAGS = -ludev -pthread

# Source files
SRCS = $(wildcard $(SRC_DIR)/*.c)
OBJS = $(patsubst $(SRC_DIR)/%.c, $(OBJ_DIR)/%.o, $(SRCS))
DEPS = $(INC_DIR)/UsbEventWatcher.Linux.h
EXEC = $(BIN_DIR)/UsbEventWatcher

# Targets
all: $(EXEC)

$(EXEC): $(OBJS)
	@mkdir -p $(BIN_DIR)
	$(CC) $(CFLAGS) $(OBJS) -o $(EXEC) $(LDFLAGS)

$(OBJ_DIR)/%.o: $(SRC_DIR)/%.c
	@mkdir -p $(OBJ_DIR)
	$(CC) $(CFLAGS) -c $< -o $@
	
$(OBJ_DIR)/UsbEventWatcher.Linux.o: $(DEPS)
$(OBJ_DIR)/main.o: $(DEPS)

debug: CFLAGS += -g
debug: clean all

clean:
	rm -rf $(OBJ_DIR) $(BIN_DIR)

.PHONY: all debug clean
